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
        await using var origin = await _fileStorage.GetAsync(document.OriginStorageKey, cancellationToken);
        return await _anGineerClient.SubmitAsync(document.FileName, origin, cancellationToken);
    }

    public async Task<AnGineerJobState> PollUntilFinishedAsync(
        string anGineerJobId,
        CancellationToken cancellationToken = default)
    {
        var deadline = DateTime.UtcNow + _pollOptions.Timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var state = await _anGineerClient.GetStateAsync(anGineerJobId, cancellationToken);
                if (state != AnGineerJobState.Processing)
                {
                    return state;
                }
            }
            catch (HttpRequestException ex) when (IsTransientHttpError(ex))
            {
                // AnGIneer 侧 keep-alive 连接被关闭导致复用旧连接收到 RST 等瞬时错误；
                // 不应把整篇文档判失败，稍后重试即可。
                _logger.LogWarning(ex, "AnGIneer 状态轮询瞬时失败，稍后重试: {JobId}", anGineerJobId);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                continue;
            }
            await Task.Delay(_pollOptions.PollInterval, cancellationToken);
        }
        throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
            .WithData("reason", "轮询超时");
    }

    public async Task CompleteAsync(
        CompareDocument document,
        string anGineerJobId,
        CancellationToken cancellationToken = default)
    {
        var package = await _anGineerClient.DownloadPackageAsync(anGineerJobId, cancellationToken);

        // v2：AnGIneer 产物（graph jsonl + meta）→ 内部适配 IR
        string irJson;
        try
        {
            irJson = AnGineerIrMapper.MapToIrJson(
                Encoding.UTF8.GetString(package.GraphJsonl),
                Encoding.UTF8.GetString(package.MetaJson),
                document.Id.ToString());
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
        await _fileStorage.UploadAsync($"{prefix}/raw/doc_blocks_graph.jsonl", new MemoryStream(package.GraphJsonl), "application/x-ndjson", cancellationToken);
        await _fileStorage.UploadAsync($"{prefix}/raw/doc_blocks_graph_meta.json", new MemoryStream(package.MetaJson), "application/json", cancellationToken);

        var irKey = $"{prefix}/ir.json"; // 内部适配 IR（非跨系统交付物）
        await _fileStorage.UploadAsync(irKey, new MemoryStream(Encoding.UTF8.GetBytes(irJson)), "application/json", cancellationToken);

        string? docMdKey = null;
        if (package.ContentMd != null)
        {
            docMdKey = $"{prefix}/content.md";
            await _fileStorage.UploadAsync(docMdKey, new MemoryStream(package.ContentMd), "text/markdown", cancellationToken);
        }

        foreach (var (path, bytes) in package.Images)
        {
            await _fileStorage.UploadAsync($"{prefix}/{path}", new MemoryStream(bytes), "application/octet-stream", cancellationToken);
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
        document.MarkParseFailed(message);
        await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
    }

    private static bool IsTransientHttpError(HttpRequestException ex)
    {
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
