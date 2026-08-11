using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// 解析后台任务（spec §5 步骤2）：下载原始文件 → 提交 AnGIneer → 轮询 → 下载产物包 →
/// AnGineerIrMapper 映射为内部适配 IR（v2 §2/§3）→ IR 校验（不合格拒收并报原因）→
/// 产物落对象存储（原始产物留档 raw/ + ir.json + content.md + images/）→ 更新文档与任务状态。
/// 失败策略（spec §9）：单份失败标记原因、任务降级 Partial，其余照常；全部失败 → Failed。
/// </summary>
public class ParseDocumentJob : AsyncBackgroundJob<ParseDocumentArgs>, ITransientDependency
{
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IRepository<CompareTask, Guid> _taskRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IAnGineerClient _anGineerClient;
    private readonly IIrValidator _irValidator;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly AnGineerPollOptions _pollOptions;

    public ParseDocumentJob(
        IRepository<CompareDocument, Guid> documentRepository,
        IRepository<CompareTask, Guid> taskRepository,
        IFileStorage fileStorage,
        IAnGineerClient anGineerClient,
        IIrValidator irValidator,
        IBackgroundJobManager backgroundJobManager,
        IOptions<AnGineerPollOptions> pollOptions)
    {
        _documentRepository = documentRepository;
        _taskRepository = taskRepository;
        _fileStorage = fileStorage;
        _anGineerClient = anGineerClient;
        _irValidator = irValidator;
        _backgroundJobManager = backgroundJobManager;
        _pollOptions = pollOptions.Value;
    }

    public override async Task ExecuteAsync(ParseDocumentArgs args)
    {
        var cancellationToken = CancellationToken.None;
        var document = await _documentRepository.FindAsync(args.DocumentId, cancellationToken: cancellationToken);
        if (document == null)
        {
            Logger.LogWarning("CompareDocument {DocumentId} 不存在，跳过解析", args.DocumentId);
            return;
        }
        var task = await _taskRepository.GetAsync(args.TaskId, cancellationToken: cancellationToken);

        try
        {
            document.MarkParsing();
            await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);

            await using var origin = await _fileStorage.GetAsync(document.OriginStorageKey, cancellationToken);
            var anGineerJobId = await _anGineerClient.SubmitAsync(document.FileName, origin, cancellationToken);

            var state = await PollUntilFinishedAsync(anGineerJobId, cancellationToken);
            if (state == AnGineerJobState.Failed)
            {
                throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                    .WithData("fileName", document.FileName);
            }

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

            var prefix = $"compare/{args.TaskId}/{args.DocumentId}";

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
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "文档 {DocumentId} 解析失败", args.DocumentId);
            document.MarkParseFailed(ex is BusinessException be && be.Code != null
                ? $"{be.Code}: {string.Join("；", be.Data.Keys.Cast<string>().Select(k => be.Data[k]))}"
                : ex.Message);
            await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
        }

        await AdvanceTaskStateAsync(task, cancellationToken);
    }

    private async Task<AnGineerJobState> PollUntilFinishedAsync(string anGineerJobId, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + _pollOptions.Timeout;
        while (DateTime.UtcNow < deadline)
        {
            var state = await _anGineerClient.GetStateAsync(anGineerJobId, cancellationToken);
            if (state != AnGineerJobState.Processing)
            {
                return state;
            }
            await Task.Delay(_pollOptions.PollInterval, cancellationToken);
        }
        throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
            .WithData("reason", "轮询超时");
    }

    /// <summary>spec §5 步骤2→3→4：全部文档落定后推进任务状态。</summary>
    private async Task AdvanceTaskStateAsync(CompareTask task, CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.GetListAsync(d => d.TaskId == task.Id);

        if (documents.Any(d => d.ParseStatus is DocumentParseStatus.Pending or DocumentParseStatus.Parsing))
        {
            task.UpdateProgress("parsing", 10 + 20 * documents.Count(d => d.ParseStatus == DocumentParseStatus.Parsed) / Math.Max(documents.Count, 1), null);
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        var failed = documents.Where(d => d.ParseStatus == DocumentParseStatus.Failed).ToList();
        var parsed = documents.Where(d => d.ParseStatus == DocumentParseStatus.Parsed).ToList();

        if (parsed.Count == 0)
        {
            // spec §9：AnGIneer 不可用/全部失败 → 明确提示，不静默降级
            task.MarkFailed("全部文档解析失败：" + string.Join("；", failed.Select(f => $"{f.FileName}: {f.ParseError}")));
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        if (failed.Count > 0)
        {
            task.MarkPartial(string.Join("；", failed.Select(f => $"{f.FileName}: {f.ParseError}")));
        }
        else
        {
            task.MarkParsed();
        }

        if (task.TenderDocumentId.HasValue && task.ClauseSnapshotJson == null)
        {
            if (task.Status != CompareTaskStatus.AwaitingClauses)
            {
                task.MarkAwaitingClauses();
            }
            task.UpdateProgress("clauses", 40, "等待条款确认");
        }
        else
        {
            if (task.Status != CompareTaskStatus.Comparing)
            {
                task.MarkComparing();
                await _backgroundJobManager.EnqueueAsync(new CompareDocumentsArgs { TaskId = task.Id });
            }
            task.UpdateProgress("comparing", 60, "两两比对中");
        }

        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
    }
}
