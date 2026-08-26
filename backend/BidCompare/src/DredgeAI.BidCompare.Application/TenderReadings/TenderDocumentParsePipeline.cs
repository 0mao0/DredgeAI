using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.BackgroundJobs;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>
/// 读标文档解析管线：提交 AnGIneer → 轮询 → 下载产物 → AnGineerIrMapper 映射内部 IR →
/// IR 校验 → 落 tender-read 前缀对象存储 → 更新文档与任务状态。
/// </summary>
public class TenderDocumentParsePipeline : ITransientDependency
{
    private readonly IRepository<TenderReadingDocument, Guid> _documentRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IAnGineerClient _anGineerClient;
    private readonly IIrValidator _irValidator;
    private readonly AnGineerPollOptions _pollOptions;
    private readonly ILogger<TenderDocumentParsePipeline> _logger;

    public TenderDocumentParsePipeline(
        IRepository<TenderReadingDocument, Guid> documentRepository,
        IFileStorage fileStorage,
        IAnGineerClient anGineerClient,
        IIrValidator irValidator,
        IOptions<AnGineerPollOptions> pollOptions,
        ILogger<TenderDocumentParsePipeline> logger)
    {
        _documentRepository = documentRepository;
        _fileStorage = fileStorage;
        _anGineerClient = anGineerClient;
        _irValidator = irValidator;
        _pollOptions = pollOptions.Value;
        _logger = logger;
    }

    public async Task MarkParsingAsync(TenderReadingDocument document, CancellationToken cancellationToken = default)
    {
        document.MarkParsing();
        await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
    }

    public async Task<string> SubmitAsync(TenderReadingDocument document, CancellationToken cancellationToken = default)
    {
        return await _anGineerClient.SubmitAsync(
            document.FileName,
            async () => await _fileStorage.GetAsync(document.OriginStorageKey, cancellationToken),
            cancellationToken);
    }

    public async Task<string> GetOrResumeJobAsync(
        TenderReadingDocument document,
        SemaphoreSlim? writeGate,
        CancellationToken cancellationToken = default)
    {
        var existing = document.AnGineerDocId?.Trim();
        if (!string.IsNullOrWhiteSpace(existing))
        {
            try
            {
                var status = await _anGineerClient.GetStateAsync(existing, cancellationToken);
                if (status.State is AnGineerJobState.Succeeded or AnGineerJobState.Partial)
                {
                    return existing;
                }

                if (status.State == AnGineerJobState.Failed && !IsInterruptionError(status))
                {
                    _logger.LogWarning(
                        "读标文档 {DocumentId} 的 AnGIneer doc_id {DocId} 处于普通失败态，退回重新上传",
                        document.Id, existing);
                }
                else
                {
                    var resumed = await _anGineerClient.ResumeAsync(existing, cancellationToken);
                    _logger.LogInformation(
                        "读标文档 {DocumentId} 复用 AnGIneer doc_id {DocId}，resume 后状态 {State}",
                        document.Id, existing, resumed.State);
                    return existing;
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("读标文档 {DocumentId} 的 AnGIneer doc_id {DocId} 不存在，退回重新上传", document.Id, existing);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("读标文档 {DocumentId} 的 AnGIneer doc_id {DocId} 无权限，退回重新上传", document.Id, existing);
            }
        }

        var jobId = await SubmitAsync(document, cancellationToken);
        if (writeGate != null)
        {
            await writeGate.WaitAsync(cancellationToken);
        }

        try
        {
            document.SetAnGineerDocId(jobId);
            await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
        }
        finally
        {
            writeGate?.Release();
        }

        return jobId;
    }

    public async Task<AnGineerJobStatus> PollUntilFinishedAsync(
        string anGineerJobId,
        TenderReadingDocument document,
        SemaphoreSlim? writeGate,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + _pollOptions.Timeout;
        var staleResumeAttempted = false;
        var interruptedResumeAttempted = false;
        var stallResumeAttempted = false;
        string? lastSignature = null;
        DateTime? lastSignatureChangeAt = null;

        while (DateTime.UtcNow < deadline)
        {
            AnGineerJobStatus status;
            try
            {
                status = await _anGineerClient.GetStateAsync(anGineerJobId, cancellationToken);
            }
            catch (HttpRequestException ex) when (IsTransientHttpError(ex))
            {
                _logger.LogWarning(ex, "AnGIneer 状态轮询瞬时失败，稍后重试: {JobId}", anGineerJobId);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                continue;
            }

            if (ShouldResumeStaleRecord(status) && !staleResumeAttempted)
            {
                _logger.LogWarning("AnGIneer 文档 {JobId} 疑似旧记录，尝试 resume 恢复", anGineerJobId);
                await _anGineerClient.ResumeAsync(anGineerJobId, cancellationToken);
                staleResumeAttempted = true;
                stallResumeAttempted = true;
                ResetStallTracking(status, ref lastSignature, ref lastSignatureChangeAt);
                continue;
            }

            if (status.State == AnGineerJobState.Failed
                && !interruptedResumeAttempted
                && IsInterruptionError(status))
            {
                _logger.LogWarning("AnGIneer 文档 {JobId} 解析中断（{Message}），尝试 resume 恢复", anGineerJobId, status.FailureReason);
                interruptedResumeAttempted = true;
                stallResumeAttempted = true;
                await _anGineerClient.ResumeAsync(anGineerJobId, cancellationToken);
                ResetStallTracking(status, ref lastSignature, ref lastSignatureChangeAt);
                continue;
            }

            if (status.State == AnGineerJobState.Processing)
            {
                var signature = BuildProgressSignature(status);
                if (signature != lastSignature)
                {
                    lastSignature = signature;
                    lastSignatureChangeAt = DateTime.UtcNow;
                    stallResumeAttempted = false;
                }
                else if (lastSignatureChangeAt != null
                         && DateTime.UtcNow - lastSignatureChangeAt.Value >= _pollOptions.StallTimeout)
                {
                    if (!stallResumeAttempted)
                    {
                        _logger.LogWarning(
                            "AnGIneer 文档 {JobId} 解析停滞（{Signature} 在 {Minutes} 分钟内无变化），尝试 resume 恢复",
                            anGineerJobId, signature, _pollOptions.StallTimeout.TotalMinutes);
                        await _anGineerClient.ResumeAsync(anGineerJobId, cancellationToken);
                        stallResumeAttempted = true;
                        lastSignatureChangeAt = DateTime.UtcNow;
                        continue;
                    }

                    throw new BusinessException(TenderReadErrorCodes.AnGineerParseFailed)
                        .WithData("reason", $"AnGIneer 解析停滞（{signature} 在 {_pollOptions.StallTimeout.TotalMinutes:0.#} 分钟内无变化，resume 后仍无进展）");
                }
            }

            await PersistProgressAsync(document, status, writeGate, cancellationToken);
            if (status.State != AnGineerJobState.Processing)
            {
                return status;
            }

            await Task.Delay(_pollOptions.PollInterval, cancellationToken);
        }

        throw new BusinessException(TenderReadErrorCodes.AnGineerParseFailed)
            .WithData("reason", "轮询超时");
    }

    public async Task CompleteAsync(
        TenderReadingDocument document,
        string anGineerJobId,
        CancellationToken cancellationToken = default)
    {
        var artifacts = await _anGineerClient.ListArtifactsAsync(anGineerJobId, cancellationToken);
        var graphArtifact = artifacts.FirstOrDefault(a => a.Name == "doc_blocks_graph.jsonl");
        var metaArtifact = artifacts.FirstOrDefault(a => a.Name == "doc_blocks_graph_meta.json");
        if (graphArtifact == null || metaArtifact == null)
        {
            throw new BusinessException(TenderReadErrorCodes.AnGineerParseFailed)
                .WithData("reason", "产物包缺少 doc_blocks_graph.jsonl / doc_blocks_graph_meta.json");
        }

        string graphJsonl;
        await using (var stream = await _anGineerClient.OpenArtifactAsync(anGineerJobId, graphArtifact, cancellationToken))
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            graphJsonl = await reader.ReadToEndAsync(cancellationToken);
        }

        string metaJson;
        await using (var stream = await _anGineerClient.OpenArtifactAsync(anGineerJobId, metaArtifact, cancellationToken))
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            metaJson = await reader.ReadToEndAsync(cancellationToken);
        }

        string irJson;
        try
        {
            irJson = AnGineerIrMapper.MapToIrJson(graphJsonl, metaJson, document.Id.ToString());
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new BusinessException(TenderReadErrorCodes.IrValidationFailed)
                .WithData("errors", $"AnGIneer 产物映射失败：{ex.Message}");
        }

        var validation = _irValidator.Validate(irJson);
        if (!validation.IsValid)
        {
            throw new BusinessException(TenderReadErrorCodes.IrValidationFailed)
                .WithData("errors", string.Join("；", validation.Errors));
        }

        var prefix = $"tender-read/{document.TaskId}/{document.Id}";

        await _fileStorage.UploadAsync(
            $"{prefix}/raw/doc_blocks_graph.jsonl",
            new MemoryStream(Encoding.UTF8.GetBytes(graphJsonl)),
            "application/x-ndjson",
            cancellationToken);
        await _fileStorage.UploadAsync(
            $"{prefix}/raw/doc_blocks_graph_meta.json",
            new MemoryStream(Encoding.UTF8.GetBytes(metaJson)),
            "application/json",
            cancellationToken);

        var irKey = $"{prefix}/ir.json";
        await _fileStorage.UploadAsync(
            irKey,
            new MemoryStream(Encoding.UTF8.GetBytes(irJson)),
            "application/json",
            cancellationToken);

        string? docMdKey = null;
        var contentMdArtifact = artifacts.FirstOrDefault(a => a.Name == "content.md");
        if (contentMdArtifact != null)
        {
            docMdKey = $"{prefix}/content.md";
            await using var stream = await _anGineerClient.OpenArtifactAsync(anGineerJobId, contentMdArtifact, cancellationToken);
            await _fileStorage.UploadAsync(docMdKey, stream, "text/markdown", cancellationToken);
        }

        foreach (var image in artifacts.Where(a => a.Name.StartsWith("images/", StringComparison.Ordinal)))
        {
            await using var stream = await _anGineerClient.OpenArtifactAsync(anGineerJobId, image, cancellationToken);
            await _fileStorage.UploadAsync($"{prefix}/{image.Name}", stream, "application/octet-stream", cancellationToken);
        }

        using var irDocument = JsonDocument.Parse(irJson);
        var pageCount = irDocument.RootElement.GetProperty("meta").GetProperty("pageCount").GetInt32();

        document.MarkParsed(irKey, docMdKey, pageCount);
        await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
    }

    public async Task MarkFailedAsync(
        TenderReadingDocument document,
        Exception ex,
        CancellationToken cancellationToken = default)
    {
        var message = ex is BusinessException be && be.Code != null
            ? $"{be.Code}: {string.Join("；", be.Data.Keys.Cast<string>().Select(k => be.Data[k]))}"
            : ex.Message;
        document.MarkParseFailed(message);
        await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
    }

    private static string BuildProgressSignature(AnGineerJobStatus status)
        => $"{status.Progress}|{status.Stage}|{status.StageMessage}";

    private static void ResetStallTracking(
        AnGineerJobStatus status,
        ref string? lastSignature,
        ref DateTime? lastSignatureChangeAt)
    {
        lastSignature = BuildProgressSignature(status);
        lastSignatureChangeAt = DateTime.UtcNow;
    }

    private async Task PersistProgressAsync(
        TenderReadingDocument document,
        AnGineerJobStatus status,
        SemaphoreSlim? writeGate,
        CancellationToken cancellationToken)
    {
        if (writeGate != null)
        {
            await writeGate.WaitAsync(cancellationToken);
        }

        try
        {
            if (status.State == AnGineerJobState.Processing)
            {
                document.UpdateParseProgress(status.Progress, status.Stage, status.StageMessage);
            }
            else
            {
                var (stage, message) = status.State switch
                {
                    AnGineerJobState.Failed => (status.Stage ?? "failed", status.StageMessage ?? status.Error ?? "解析失败"),
                    AnGineerJobState.Partial => (status.Stage ?? "partial", status.StageMessage ?? "解析部分完成（soft 阶段失败，已尝试下载产物）"),
                    _ => (status.Stage ?? "completed", status.StageMessage ?? "解析结束")
                };
                document.UpdateParseProgress(100, stage, message);
            }

            await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
        }
        finally
        {
            writeGate?.Release();
        }
    }

    private static bool ShouldResumeStaleRecord(AnGineerJobStatus status)
        => status.State == AnGineerJobState.Processing
           && status.Progress == 0
           && status.Stage is "pending" or "processing"
           && string.IsNullOrWhiteSpace(status.StageMessage);

    private static bool IsInterruptionError(AnGineerJobStatus status)
    {
        var message = status.FailureReason;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.Contains("中断", StringComparison.Ordinal)
               || message.Contains("/resume", StringComparison.OrdinalIgnoreCase)
               || message.Contains("interrupt", StringComparison.OrdinalIgnoreCase)
               || message.Contains("restart", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransientHttpError(HttpRequestException ex)
    {
        if (ex.StatusCode is >= System.Net.HttpStatusCode.InternalServerError
            or System.Net.HttpStatusCode.RequestTimeout
            or System.Net.HttpStatusCode.TooManyRequests)
        {
            return true;
        }

        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner is IOException or System.Net.Sockets.SocketException)
            {
                return true;
            }
        }

        return false;
    }
}
