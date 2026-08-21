using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.TenderReadings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// 进程重启恢复：把仍处于 Parsing 且已有 AnGIneer doc_id 的文档重新入队续跑。
/// ParseDocumentJob/ParseTenderDocumentJob 会先查 AnGIneer 状态，processing/failed 时调 resume，避免重新上传文件。
/// 仅恢复“近期”启动的解析（ParseStartedAt 在 DocumentParsingTimeout 内）；
/// 长期停滞的文档直接标记失败并推进所属任务状态，避免重启反复复活同一卡死任务（2026-08-18 事故）。
/// 比标线与读标线同等覆盖。
/// </summary>
public class ParseRecoveryService : ITransientDependency
{
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IRepository<TenderReadingDocument, Guid> _tenderDocumentRepository;
    private readonly IRepository<TenderReadingTask, Guid> _tenderTaskRepository;
    private readonly ParseTaskStateAdvancer _advancer;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly WatchdogOptions _watchdogOptions;
    private readonly ILogger<ParseRecoveryService> _logger;

    public ParseRecoveryService(
        IRepository<CompareDocument, Guid> documentRepository,
        IRepository<TenderReadingDocument, Guid> tenderDocumentRepository,
        IRepository<TenderReadingTask, Guid> tenderTaskRepository,
        ParseTaskStateAdvancer advancer,
        IBackgroundJobManager backgroundJobManager,
        IOptions<WatchdogOptions> watchdogOptions,
        ILogger<ParseRecoveryService> logger)
    {
        _documentRepository = documentRepository;
        _tenderDocumentRepository = tenderDocumentRepository;
        _tenderTaskRepository = tenderTaskRepository;
        _advancer = advancer;
        _backgroundJobManager = backgroundJobManager;
        _watchdogOptions = watchdogOptions.Value;
        _logger = logger;
    }

    public Task RecoverAsync(CancellationToken cancellationToken = default)
        => RecoverAsync(DateTime.UtcNow, cancellationToken);

    /// <summary>now 参数供测试注入固定时间点。</summary>
    public async Task RecoverAsync(DateTime now, CancellationToken cancellationToken = default)
    {
        var deadline = now - _watchdogOptions.DocumentParsingTimeout;
        await RecoverCompareDocumentsAsync(deadline, cancellationToken);
        await RecoverTenderDocumentsAsync(deadline, cancellationToken);
    }

    private async Task RecoverCompareDocumentsAsync(DateTime deadline, CancellationToken cancellationToken)
    {
        var documents = await _documentRepository.GetListAsync(
            d => d.ParseStatus == DocumentParseStatus.Parsing && d.AnGineerDocId != null,
            cancellationToken: cancellationToken);
        var targets = documents
            .Where(d => !string.IsNullOrWhiteSpace(d.AnGineerDocId))
            .ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var hopeless = targets
            .Where(d => d.ParseStartedAt == null || d.ParseStartedAt.Value < deadline)
            .ToList();
        foreach (var document in hopeless)
        {
            var reason =
                $"启动恢复：解析自 {document.ParseStartedAt:O} 起已超过 {_watchdogOptions.DocumentParsingTimeout.TotalMinutes} 分钟仍无终态，按停滞处理";
            _logger.LogWarning("文档 {DocumentId} {Reason}，直接标记失败（不再入队）", document.Id, reason);
            document.MarkParseFailed(reason);
            await _documentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
        }
        foreach (var taskId in hopeless.Select(d => d.TaskId).Distinct())
        {
            try
            {
                // 无望文档落定后推进任务状态，避免任务永久卡 Parsing
                await _advancer.AdvanceAsync(taskId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "任务 {TaskId} 恢复推进失败", taskId);
            }
        }

        var resumable = targets
            .Where(d => d.ParseStartedAt != null && d.ParseStartedAt.Value >= deadline)
            .ToList();
        if (resumable.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "启动恢复：发现 {Count} 个解析中且已有 AnGIneer doc_id 的近期文档，重新入队续跑",
            resumable.Count);
        foreach (var document in resumable)
        {
            await _backgroundJobManager.EnqueueAsync(new ParseDocumentArgs
            {
                TaskId = document.TaskId,
                DocumentId = document.Id
            });
        }
    }

    /// <summary>读标线恢复：与比标同策略——近期停滞续跑，长期停滞标失败并落定任务终态。</summary>
    private async Task RecoverTenderDocumentsAsync(DateTime deadline, CancellationToken cancellationToken)
    {
        var documents = await _tenderDocumentRepository.GetListAsync(
            d => d.ParseStatus == DocumentParseStatus.Parsing && d.AnGineerDocId != null,
            cancellationToken: cancellationToken);
        var targets = documents
            .Where(d => !string.IsNullOrWhiteSpace(d.AnGineerDocId))
            .ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var hopeless = targets
            .Where(d => d.ParseStartedAt == null || d.ParseStartedAt.Value < deadline)
            .ToList();
        foreach (var document in hopeless)
        {
            var reason =
                $"启动恢复：解析自 {document.ParseStartedAt:O} 起已超过 {_watchdogOptions.DocumentParsingTimeout.TotalMinutes} 分钟仍无终态，按停滞处理";
            _logger.LogWarning("读标文档 {DocumentId} {Reason}，直接标记失败（不再入队）", document.Id, reason);
            document.MarkParseFailed(reason);
            await _tenderDocumentRepository.UpdateAsync(document, autoSave: true, cancellationToken: cancellationToken);
        }
        foreach (var taskId in hopeless.Select(d => d.TaskId).Distinct())
        {
            await FailTenderTaskIfNoParsingLeftAsync(taskId, "解析停滞超时（启动恢复自动标记）", cancellationToken);
        }

        var resumable = targets
            .Where(d => d.ParseStartedAt != null && d.ParseStartedAt.Value >= deadline)
            .ToList();
        if (resumable.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "启动恢复：发现 {Count} 个解析中的近期读标文档，重新入队续跑",
            resumable.Count);
        foreach (var document in resumable)
        {
            await _backgroundJobManager.EnqueueAsync(new ParseTenderDocumentArgs
            {
                TaskId = document.TaskId,
                DocumentId = document.Id
            });
        }
    }

    /// <summary>读标任务无独立状态推进器：文档停滞落定后若已无解析中文档，直接把任务落定失败。</summary>
    private async Task FailTenderTaskIfNoParsingLeftAsync(Guid taskId, string reason, CancellationToken cancellationToken)
    {
        var task = await _tenderTaskRepository.FindAsync(taskId, cancellationToken: cancellationToken);
        if (task == null || task.Status is TenderReadingTaskStatus.Ready or TenderReadingTaskStatus.Failed)
        {
            return;
        }
        var remaining = await _tenderDocumentRepository.CountAsync(
            d => d.TaskId == taskId &&
                 (d.ParseStatus == DocumentParseStatus.Pending || d.ParseStatus == DocumentParseStatus.Parsing),
            cancellationToken: cancellationToken);
        if (remaining > 0)
        {
            return;
        }
        try
        {
            task.MarkFailed(reason);
            await _tenderTaskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "读标任务 {TaskId} 恢复落定失败（状态 {Status}）", taskId, task.Status);
        }
    }
}
