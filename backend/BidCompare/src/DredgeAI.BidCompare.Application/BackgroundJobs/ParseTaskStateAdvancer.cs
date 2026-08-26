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
using Volo.Abp.Uow;

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
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly ILogger<ParseTaskStateAdvancer> _logger;

    public ParseTaskStateAdvancer(
        IRepository<CompareDocument, Guid> documentRepository,
        IRepository<CompareTask, Guid> taskRepository,
        IFileStorage fileStorage,
        IBackgroundJobManager backgroundJobManager,
        IUnitOfWorkManager unitOfWorkManager,
        ILogger<ParseTaskStateAdvancer> logger)
    {
        _documentRepository = documentRepository;
        _taskRepository = taskRepository;
        _fileStorage = fileStorage;
        _backgroundJobManager = backgroundJobManager;
        _unitOfWorkManager = unitOfWorkManager;
        _logger = logger;
    }

    /// <summary>
    /// 推进任务状态。多文档/多 Job 并发推进是常态，任务行的 ConcurrencyStamp 冲突属预期：
    /// 每次尝试都在独立工作单元内重读最新实体后按最新状态重放推进（推进是状态函数，重放安全）。
    /// </summary>
    public async Task AdvanceAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
            try
            {
                await AdvanceOnceAsync(taskId, cancellationToken);
                await uow.CompleteAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && DbConcurrency.IsConflict(ex))
            {
                _logger.LogDebug("任务 {TaskId} 状态推进并发冲突，重读重试（第 {Attempt}/{MaxAttempts} 次）",
                    taskId, attempt, maxAttempts);
            }
        }
    }

    private async Task AdvanceOnceAsync(Guid taskId, CancellationToken cancellationToken)
    {
        var task = await _taskRepository.GetAsync(taskId, cancellationToken: cancellationToken);
        // 比对/分析/终态的任务不由解析推进器处理（看门狗与恢复器可能触发本方法）
        if (task.Status is CompareTaskStatus.Comparing or CompareTaskStatus.Analyzing
            or CompareTaskStatus.Done or CompareTaskStatus.Failed)
        {
            return;
        }
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

        // 先落库后入队：并发下若 ConcurrencyStamp 冲突导致落库失败，不会残留已入队的孤儿任务
        var shouldEnqueueCompare = task.Status != CompareTaskStatus.Comparing;
        if (shouldEnqueueCompare)
        {
            task.MarkComparing();
        }
        task.UpdateProgress("comparing", 60, "两两比对中");

        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
        if (shouldEnqueueCompare)
        {
            await _backgroundJobManager.EnqueueAsync(new CompareDocumentsArgs { TaskId = task.Id });
        }
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
