using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// spec §5 步骤2→3→4：全部文档落定后推进任务状态。
/// v2：可用标书不足 2 份不进入比对；重新解析后不自动重跑全量对比，由用户显式触发。
/// </summary>
public class ParseTaskStateAdvancer : ITransientDependency
{
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IRepository<CompareTask, Guid> _taskRepository;
    private readonly IFileStorage _fileStorage;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly ILogger<ParseTaskStateAdvancer> _logger;

    public ParseTaskStateAdvancer(
        IRepository<CompareDocument, Guid> documentRepository,
        IRepository<CompareTask, Guid> taskRepository,
        IFileStorage fileStorage,
        IBackgroundJobManager backgroundJobManager,
        ILogger<ParseTaskStateAdvancer> logger)
    {
        _documentRepository = documentRepository;
        _taskRepository = taskRepository;
        _fileStorage = fileStorage;
        _backgroundJobManager = backgroundJobManager;
        _logger = logger;
    }

    public async Task AdvanceAsync(CompareTask task, CancellationToken cancellationToken = default)
    {
        var documents = await _documentRepository.GetListAsync(d => d.TaskId == task.Id, cancellationToken: cancellationToken);

        if (documents.Any(d => d.ParseStatus is DocumentParseStatus.Pending or DocumentParseStatus.Parsing))
        {
            // 进度严格按已完成份数计算：0 份完成就是 0%，避免“还没解析就显示 10%”的误导
            var parsedCount = documents.Count(d => d.ParseStatus == DocumentParseStatus.Parsed);
            task.UpdateProgress("parsing", parsedCount * 100 / Math.Max(documents.Count, 1), null);
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

        var parsedBids = parsed.Where(d => d.Role == DocumentRole.Bid).ToList();

        if (failed.Count > 0)
        {
            task.MarkPartial(string.Join("；", failed.Select(f => $"{f.FileName}: {f.ParseError}")));
        }
        else
        {
            task.MarkParsed();
        }

        // 项目名建议：招标文档优先，其次首份解析成功的标书；仅填充一次，不覆盖任务名（spec §3.3）
        if (task.SuggestedName.IsNullOrWhiteSpace())
        {
            var titleSource = task.TenderDocumentId.HasValue
                ? parsed.FirstOrDefault(d => d.Id == task.TenderDocumentId)
                : parsedBids.FirstOrDefault();
            if (titleSource != null)
            {
                task.SetSuggestedName(await ReadSuggestedNameAsync(titleSource, cancellationToken));
            }
        }

        var canCompare = parsedBids.Count >= 2;

        if (task.TenderDocumentId.HasValue && task.ClauseSnapshotJson == null)
        {
            if (task.Status != CompareTaskStatus.AwaitingClauses)
            {
                task.MarkAwaitingClauses();
            }
            task.UpdateProgress("clauses", 40, canCompare ? "等待条款确认" : "可用标书不足 2 份，请重新解析失败文档");
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        // v2 §5.3：重新解析成功后不自动重跑全量对比，避免静默改变既有报告
        if (!canCompare || !task.AutoCompareOnParseComplete)
        {
            task.UpdateProgress("parsing", 100, canCompare ? "解析完成，等待重新对比" : "可用标书不足 2 份，请重新解析失败文档");
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        if (task.Status != CompareTaskStatus.Comparing)
        {
            task.MarkComparing();
            await _backgroundJobManager.EnqueueAsync(new CompareDocumentsArgs { TaskId = task.Id });
        }
        task.UpdateProgress("comparing", 60, "两两比对中");

        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
    }

    /// <summary>从 IR 读取项目名建议：outline 首节点标题优先，其次 meta.fileName 去扩展名。</summary>
    private async Task<string?> ReadSuggestedNameAsync(CompareDocument document, CancellationToken cancellationToken)
    {
        if (document.IrStorageKey == null)
        {
            return null;
        }
        try
        {
            await using var stream = await _fileStorage.GetAsync(document.IrStorageKey, cancellationToken);
            using var ir = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            if (ir.RootElement.TryGetProperty("outline", out var outline) &&
                outline.ValueKind == JsonValueKind.Array &&
                outline.GetArrayLength() > 0 &&
                outline[0].TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(title.GetString()))
            {
                return title.GetString()!.Trim();
            }
            if (ir.RootElement.TryGetProperty("meta", out var meta) &&
                meta.TryGetProperty("fileName", out var fileName) &&
                fileName.ValueKind == JsonValueKind.String)
            {
                return Path.GetFileNameWithoutExtension(fileName.GetString());
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "读取文档 {DocumentId} 建议名失败，忽略", document.Id);
        }
        return null;
    }
}
