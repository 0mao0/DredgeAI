using System;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.TenderReadings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// 卡死看门狗（spec §9 补漏）：周期性巡检无自愈通道的中间态——
/// 文档 Parsing 超时 → 标记失败并重新推进任务状态；
/// 任务 Comparing/Analyzing 超时 → 标记失败（处理中的比对对一并落失败）。
/// 阈值经 Watchdog 配置节可调。
/// </summary>
public class StuckTaskWatchdogWorker : AsyncPeriodicBackgroundWorkerBase, ITransientDependency
{
    private readonly WatchdogOptions _options;
    private readonly IClock _clock;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public StuckTaskWatchdogWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<WatchdogOptions> options,
        IClock clock,
        IBackgroundJobManager backgroundJobManager)
        : base(timer, serviceScopeFactory)
    {
        _options = options.Value;
        _clock = clock;
        _backgroundJobManager = backgroundJobManager;
        Timer.Period = (int)_options.Period.TotalMilliseconds;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        // ParseStartedAt 由 MarkParsing 写 DateTime.UtcNow（UTC 朴素值），巡检必须用 UTC 对齐；
        // 否则本地时间与 UTC 相差 8 小时，刚启动的解析会被误判为“已超时 35 分钟”（2026-08-19 事故）。
        await SweepAsync(workerContext.ServiceProvider, DateTime.UtcNow);
    }

    /// <summary>单次巡检（internal 供测试以受控时间点直接驱动）。</summary>
    internal async Task SweepAsync(IServiceProvider serviceProvider, DateTime utcNow)
    {
        var documentRepository = serviceProvider.GetRequiredService<IRepository<CompareDocument, Guid>>();
        var taskRepository = serviceProvider.GetRequiredService<IRepository<CompareTask, Guid>>();
        var advancer = serviceProvider.GetRequiredService<ParseTaskStateAdvancer>();

        var docDeadline = utcNow - _options.DocumentParsingTimeout;
        var stuckDocuments = await documentRepository.GetListAsync(d =>
            d.ParseStatus == DocumentParseStatus.Parsing &&
            d.ParseStartedAt != null &&
            d.ParseStartedAt < docDeadline &&
            d.AnGineerDocId == null);
        var resumableStuckDocuments = await documentRepository.GetListAsync(d =>
            d.ParseStatus == DocumentParseStatus.Parsing &&
            d.AnGineerDocId != null &&
            d.ParseStartedAt != null &&
            d.ParseStartedAt < docDeadline);
        foreach (var document in resumableStuckDocuments)
        {
            Logger.LogWarning(
                "文档 {DocumentId} 解析超时，但已有 AnGIneer doc_id，尝试恢复续跑",
                document.Id);
            document.MarkParsing();
            await documentRepository.UpdateAsync(document, autoSave: true);
            await _backgroundJobManager.EnqueueAsync(new ParseDocumentArgs
            {
                TaskId = document.TaskId,
                DocumentId = document.Id
            });
        }
        foreach (var document in stuckDocuments)
        {
            Logger.LogWarning("文档 {DocumentId} 解析超时（>{Timeout}），看门狗标记失败", document.Id, _options.DocumentParsingTimeout);
            document.MarkParseFailed($"解析超时（看门狗自动标记，超时阈值 {_options.DocumentParsingTimeout.TotalMinutes} 分钟）");
            await documentRepository.UpdateAsync(document, autoSave: true);
        }
        foreach (var taskId in stuckDocuments.Select(d => d.TaskId).Distinct())
        {
            var task = await taskRepository.FindAsync(taskId);
            if (task?.Status == CompareTaskStatus.Parsing)
            {
                await advancer.AdvanceAsync(taskId);
            }
        }

        // LastModificationTime 由 ABP IClock 写入（本地时间），任务超时判断用 IClock.Now 对齐。
        var taskDeadline = _clock.Now - _options.TaskTimeout;
        var stuckTasks = await taskRepository.GetListAsync(t =>
            (t.Status == CompareTaskStatus.Comparing || t.Status == CompareTaskStatus.Analyzing) &&
            t.LastModificationTime != null &&
            t.LastModificationTime < taskDeadline);
        foreach (var task in stuckTasks)
        {
            Logger.LogWarning("任务 {TaskId} 在 {Status} 超时（>{Timeout}），看门狗标记失败", task.Id, task.Status, _options.TaskTimeout);
            foreach (var pair in task.GetPairs().Where(p => p.Status == ComparePairStatus.Processing))
            {
                task.MarkPairFailed(pair.PairId, _clock.Now, "比对超时（看门狗自动标记）");
            }
            task.MarkFailed($"比对/分析超时（看门狗自动标记，超时阈值 {_options.TaskTimeout.TotalMinutes} 分钟）");
            await taskRepository.UpdateAsync(task, autoSave: true);
        }

        await SweepTenderReadingsAsync(serviceProvider, utcNow, taskDeadline);
    }

    /// <summary>读标线巡检：与比标同策略——文档超时标失败/续跑恢复，任务中间态超时落定失败。</summary>
    private async Task SweepTenderReadingsAsync(IServiceProvider serviceProvider, DateTime utcNow, DateTime taskDeadline)
    {
        var documentRepository = serviceProvider.GetRequiredService<IRepository<TenderReadingDocument, Guid>>();
        var taskRepository = serviceProvider.GetRequiredService<IRepository<TenderReadingTask, Guid>>();

        var docDeadline = utcNow - _options.DocumentParsingTimeout;
        var stuckDocuments = await documentRepository.GetListAsync(d =>
            d.ParseStatus == DocumentParseStatus.Parsing &&
            d.ParseStartedAt != null &&
            d.ParseStartedAt < docDeadline);
        var failedTaskIds = stuckDocuments.Select(d => d.TaskId).Distinct().ToList();
        foreach (var document in stuckDocuments)
        {
            if (document.AnGineerDocId != null)
            {
                // DocumentParsingTimeout 恒大于 AnGIneer 轮询总上限，走到这里原 Job 已死亡，安全续跑
                Logger.LogWarning("读标文档 {DocumentId} 解析超时，但已有 AnGIneer doc_id，尝试恢复续跑", document.Id);
                document.MarkParsing();
                await documentRepository.UpdateAsync(document, autoSave: true);
                await _backgroundJobManager.EnqueueAsync(new ParseTenderDocumentArgs
                {
                    TaskId = document.TaskId,
                    DocumentId = document.Id
                });
                failedTaskIds.Remove(document.TaskId);
            }
            else
            {
                Logger.LogWarning("读标文档 {DocumentId} 解析超时（>{Timeout}），看门狗标记失败", document.Id, _options.DocumentParsingTimeout);
                document.MarkParseFailed($"解析超时（看门狗自动标记，超时阈值 {_options.DocumentParsingTimeout.TotalMinutes} 分钟）");
                await documentRepository.UpdateAsync(document, autoSave: true);
            }
        }
        foreach (var taskId in failedTaskIds)
        {
            var task = await taskRepository.FindAsync(taskId);
            if (task?.Status != TenderReadingTaskStatus.Parsing)
            {
                continue;
            }
            // CountAsync(predicate) 是扩展方法，看门狗无环境 UoW，用 GetListAsync 内存计数
            var unsettled = await documentRepository.GetListAsync(d =>
                d.TaskId == taskId &&
                (d.ParseStatus == DocumentParseStatus.Pending || d.ParseStatus == DocumentParseStatus.Parsing));
            if (unsettled.Count == 0)
            {
                Logger.LogWarning("读标任务 {TaskId} 全部文档解析超时，看门狗标记失败", taskId);
                task.MarkFailed("解析超时（看门狗自动标记）");
                await taskRepository.UpdateAsync(task, autoSave: true);
            }
        }

        // 抽取中卡死（ExtractBaselineJob 崩溃且重试耗尽）等无文档侧信号的中间态，按任务超时兜底
        var stuckTasks = await taskRepository.GetListAsync(t =>
            (t.Status == TenderReadingTaskStatus.Parsing || t.Status == TenderReadingTaskStatus.Extracting) &&
            t.LastModificationTime != null &&
            t.LastModificationTime < taskDeadline);
        foreach (var task in stuckTasks)
        {
            Logger.LogWarning("读标任务 {TaskId} 在 {Status} 超时（>{Timeout}），看门狗标记失败", task.Id, task.Status, _options.TaskTimeout);
            try
            {
                task.MarkFailed($"解析/抽取超时（看门狗自动标记，超时阈值 {_options.TaskTimeout.TotalMinutes} 分钟）");
                await taskRepository.UpdateAsync(task, autoSave: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "读标任务 {TaskId} 超时落定失败（状态 {Status}）", task.Id, task.Status);
            }
        }

        // 解析完成但抽取从未启动（入队失败 / Job 崩溃且未推进到 Extracting）：
        // 这类任务没有任何文档侧信号，且滞留 Parsed 用户侧只会看到“已解析 + 空基准库”，
        // 用短阈值自动补拉抽取任务，而不是等 90 分钟超时后才落失败。
        var extractRecoveryDeadline = _clock.Now - _options.TenderReadExtractRecoveryInterval;
        var parsedStuckTasks = await taskRepository.GetListAsync(t =>
            t.Status == TenderReadingTaskStatus.Parsed &&
            t.LastModificationTime != null &&
            t.LastModificationTime < extractRecoveryDeadline);
        foreach (var task in parsedStuckTasks)
        {
            var parsedDoc = (await documentRepository.GetListAsync(d =>
                    d.TaskId == task.Id &&
                    d.ParseStatus == DocumentParseStatus.Parsed &&
                    d.IrStorageKey != null))
                .OrderBy(d => d.CreationTime)
                .FirstOrDefault();
            if (parsedDoc == null)
            {
                Logger.LogWarning("读标任务 {TaskId} 解析完成但缺少 IR 产物，看门狗标记失败", task.Id);
                try
                {
                    task.MarkFailed("解析完成但缺少解析产物（看门狗自动标记）");
                    await taskRepository.UpdateAsync(task, autoSave: true);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "读标任务 {TaskId} 失败态落库失败", task.Id);
                }
                continue;
            }

            Logger.LogWarning(
                "读标任务 {TaskId} 解析完成但抽取未启动（超过 {Minutes} 分钟），看门狗重新入队抽取",
                task.Id,
                _options.TenderReadExtractRecoveryInterval.TotalMinutes);
            // 写入一次 ExtraProperties 使 LastModificationTime 刷新，作为本次恢复的冷却信号，
            // 避免队列延迟时下一轮巡检重复入队造成 LLM 重复计费。
            task.ExtraProperties["TenderReadExtractRecoveryTick"] = DateTime.UtcNow.Ticks;
            try
            {
                await taskRepository.UpdateAsync(task, autoSave: true);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "读标任务 {TaskId} 恢复标记落库失败，仍尝试入队抽取", task.Id);
            }

            await _backgroundJobManager.EnqueueAsync(new ExtractBaselineArgs
            {
                TaskId = task.Id,
                DocumentId = parsedDoc.Id
            });
        }
    }
}
