using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// 单份文档解析管线（提交 AnGIneer → 轮询 → 下载产物 → 映射/校验 → 落库），
/// 供单文档任务与批量并发任务共用。批量任务中 DB/存储写入须串行（EF Core DbContext 非线程安全）。
/// </summary>
public class DocumentParsePipeline : ITransientDependency
{
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IAnGineerClient _anGineerClient;
    private readonly IIrValidator _irValidator;
    private readonly AnGineerPollOptions _pollOptions;
    private readonly ILogger<DocumentParsePipeline> _logger;

    public DocumentParsePipeline(
        IRepository<CompareDocument, Guid> documentRepository,
        IFileStorage fileStorage,
        IAnGineerClient anGineerClient,
        IIrValidator irValidator,
        IOptions<AnGineerPollOptions> pollOptions,
        ILogger<DocumentParsePipeline> logger)
    {
        _documentRepository = documentRepository;
        _fileStorage = fileStorage;
        _anGineerClient = anGineerClient;
        _irValidator = irValidator;
        _pollOptions = pollOptions.Value;
        _logger = logger;
    }

    public async Task MarkParsingAsync(CompareDocument document, CancellationToken cancellationToken = default)
    {
        document.MarkParsing();
        await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
    }

    public async Task<string> SubmitAsync(CompareDocument document, CancellationToken cancellationToken = default)
    {
        // 流工厂：重试时重新打开存储流（避免 StreamContent 释放后复用已关闭流）
        return await _anGineerClient.SubmitAsync(
            document.FileName,
            async () => await _fileStorage.GetAsync(document.OriginStorageKey, cancellationToken),
            cancellationToken);
    }

    /// <summary>
    /// 取回或恢复 AnGIneer 任务：已有 doc_id 时先查状态，processing/failed 调 resume；
    /// doc_id 不存在（404）或不属于当前 API Key（403，AnGIneer 权限改造后旧记录不可复用）时退回重新上传。
    /// </summary>
    public async Task<string> GetOrResumeJobAsync(
        CompareDocument document,
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
                var resumed = await _anGineerClient.ResumeAsync(existing, cancellationToken);
                _logger.LogInformation(
                    "文档 {DocumentId} 复用 AnGIneer doc_id {DocId}，resume 后状态 {State}",
                    document.Id, existing, resumed.State);
                return existing;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning(
                    "文档 {DocumentId} 的 AnGIneer doc_id {DocId} 不存在（404），退回重新上传",
                    document.Id, existing);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                _logger.LogWarning(
                    "文档 {DocumentId} 的 AnGIneer doc_id {DocId} 无权限（403，属于其他 API Key/知识库），退回重新上传",
                    document.Id, existing);
            }
            catch (BusinessException be) when (be.Code == BidCompareErrorCodes.AnGineerParseFailed
                                               && be.Data.Values.Cast<string?>().Any(v => v?.Contains("403") == true))
            {
                _logger.LogWarning(
                    "文档 {DocumentId} 的 AnGIneer doc_id {DocId} resume 无权限（403），退回重新上传",
                    document.Id, existing);
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
        CompareDocument document,
        SemaphoreSlim? writeGate,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + _pollOptions.Timeout;
        var staleResumeAttempted = false;
        while (DateTime.UtcNow < deadline)
        {
            AnGineerJobStatus status;
            try
            {
                status = await _anGineerClient.GetStateAsync(anGineerJobId, cancellationToken);
            }
            catch (HttpRequestException ex) when (IsTransientHttpError(ex))
            {
                // AnGIneer 侧 keep-alive 连接被关闭导致复用旧连接收到 RST 等瞬时错误；
                // 不应把整篇文档判失败，稍后重试即可。
                _logger.LogWarning(ex, "AnGIneer 状态轮询瞬时失败，稍后重试: {JobId}", anGineerJobId);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                continue;
            }

            if (ShouldResumeStaleRecord(status) && !staleResumeAttempted)
            {
                _logger.LogWarning(
                    "AnGIneer 文档 {JobId} 疑似旧记录（progress=0 + 空阶段消息），尝试 resume 恢复",
                    anGineerJobId);
                await _anGineerClient.ResumeAsync(anGineerJobId, cancellationToken);
                staleResumeAttempted = true;
                continue;
            }

            await PersistProgressAsync(document, status, writeGate, cancellationToken);
            if (status.State != AnGineerJobState.Processing)
            {
                return status;
            }
            await Task.Delay(_pollOptions.PollInterval, cancellationToken);
        }
        throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
            .WithData("reason", "轮询超时");
    }

    /// <summary>把 AnGIneer 进度快照同步到 CompareDocument（批量解析时写库统一串行）。</summary>
    private async Task PersistProgressAsync(
        CompareDocument document,
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
                    AnGineerJobState.Failed => (
                        status.Stage ?? "failed",
                        status.StageMessage ?? status.Error ?? "解析失败"),
                    // soft 阶段（vectors/graph 等）失败时 AnGIneer 返回 partial；
                    // 核心结构产物（jsonl/meta）通常仍完整，后续按正常产物下载。
                    AnGineerJobState.Partial => (
                        status.Stage ?? "partial",
                        status.StageMessage ?? "解析部分完成（soft 阶段失败，已尝试下载产物）"),
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

    public async Task CompleteAsync(
        CompareDocument document,
        string anGineerJobId,
        CancellationToken cancellationToken = default)
    {
        var artifacts = await _anGineerClient.ListArtifactsAsync(anGineerJobId, cancellationToken);
        var graphArtifact = artifacts.FirstOrDefault(a => a.Name == "doc_blocks_graph.jsonl");
        var metaArtifact = artifacts.FirstOrDefault(a => a.Name == "doc_blocks_graph_meta.json");
        if (graphArtifact == null || metaArtifact == null)
        {
            throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                .WithData("reason", "产物包缺少 doc_blocks_graph.jsonl / doc_blocks_graph_meta.json");
        }

        // graph/meta 需全文参与 IR 映射，逐份读取后即释放
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

        // v2：AnGIneer 产物（graph jsonl + meta）→ 内部适配 IR
        string irJson;
        try
        {
            irJson = AnGineerIrMapper.MapToIrJson(graphJsonl, metaJson, document.Id.ToString());
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                .WithData("errors", $"AnGIneer 产物映射失败：{ex.Message}");
        }

        var validation = _irValidator.Validate(irJson);
        if (!validation.IsValid)
        {
            throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                .WithData("errors", string.Join("；", validation.Errors));
        }

        var prefix = $"compare/{document.TaskId}/{document.Id}";

        // AnGIneer 原始产物留档（追溯/调试，v2 §1 数据源原样保存）
        await _fileStorage.UploadAsync($"{prefix}/raw/doc_blocks_graph.jsonl", new MemoryStream(Encoding.UTF8.GetBytes(graphJsonl)), "application/x-ndjson", cancellationToken);
        await _fileStorage.UploadAsync($"{prefix}/raw/doc_blocks_graph_meta.json", new MemoryStream(Encoding.UTF8.GetBytes(metaJson)), "application/json", cancellationToken);

        var irKey = $"{prefix}/ir.json"; // 内部适配 IR（非跨系统交付物）
        await _fileStorage.UploadAsync(irKey, new MemoryStream(Encoding.UTF8.GetBytes(irJson)), "application/json", cancellationToken);

        // content.md / images 目前 AnGIneer v1 产物清单尚未开放（仅 graph/meta）；
        // 清单里一旦出现即随包流式落存储，避免后续再改适配层。
        string? docMdKey = null;
        var contentMdArtifact = artifacts.FirstOrDefault(a => a.Name == "content.md");
        if (contentMdArtifact != null)
        {
            docMdKey = $"{prefix}/content.md";
            await using var stream = await _anGineerClient.OpenArtifactAsync(anGineerJobId, contentMdArtifact, cancellationToken);
            await _fileStorage.UploadAsync(docMdKey, stream, "text/markdown", cancellationToken);
        }

        foreach (var image in artifacts.Where(a =>
                     a.Name.StartsWith("images/", StringComparison.Ordinal)))
        {
            await using var stream = await _anGineerClient.OpenArtifactAsync(anGineerJobId, image, cancellationToken);
            await _fileStorage.UploadAsync($"{prefix}/{image.Name}", stream, "application/octet-stream", cancellationToken);
        }

        using var irDocument = JsonDocument.Parse(irJson);
        var pageCount = irDocument.RootElement.GetProperty("meta").GetProperty("pageCount").GetInt32();
        var ocrRatio = IrValidator.CalculateOcrLowConfidenceRatio(irDocument.RootElement);

        document.MarkParsed(irKey, docMdKey, pageCount, ocrRatio);
        await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
    }

    public async Task MarkFailedAsync(
        CompareDocument document,
        Exception ex,
        CancellationToken cancellationToken = default)
    {
        var message = ex is BusinessException be && be.Code != null
            ? $"{be.Code}: {string.Join("；", be.Data.Keys.Cast<string>().Select(k => be.Data[k]))}"
            : ex.Message;
        document.UpdateParseProgress(100, "failed", message);
        document.MarkParseFailed(message);
        await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>疑似 docs-api 重启遗留的 processing 记录：progress=0 + 空阶段消息。</summary>
    private static bool ShouldResumeStaleRecord(AnGineerJobStatus status)
        => status.State == AnGineerJobState.Processing
           && status.Progress == 0
           && status.Stage is "pending" or "processing"
           && string.IsNullOrWhiteSpace(status.StageMessage);

    private static bool IsTransientHttpError(HttpRequestException ex)
    {
        // 服务端 5xx / 408 / 429 同样视为瞬时错误（重试可恢复），不应直接判文档失败
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
