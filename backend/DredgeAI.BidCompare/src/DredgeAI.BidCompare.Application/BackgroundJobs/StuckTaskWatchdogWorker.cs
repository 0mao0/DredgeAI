using System;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
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
public class StuckTaskWatchdogWorker : AsyncPeriodicBackgroundWorkerBase
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
        await SweepAsync(workerContext.ServiceProvider, _clock.Now);
    }

    /// <summary>单次巡检（internal 供测试以受控时间点直接驱动）。</summary>
    internal async Task SweepAsync(IServiceProvider serviceProvider, DateTime now)
    {
        var documentRepository = serviceProvider.GetRequiredService<IRepository<CompareDocument, Guid>>();
        var taskRepository = serviceProvider.GetRequiredService<IRepository<CompareTask, Guid>>();
        var advancer = serviceProvider.GetRequiredService<ParseTaskStateAdvancer>();

        var docDeadline = now - _options.DocumentParsingTimeout;
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
                await advancer.AdvanceAsync(task);
            }
        }

        var taskDeadline = now - _options.TaskTimeout;
        var stuckTasks = await taskRepository.GetListAsync(t =>
            (t.Status == CompareTaskStatus.Comparing || t.Status == CompareTaskStatus.Analyzing) &&
            t.LastModificationTime != null &&
            t.LastModificationTime < taskDeadline);
        foreach (var task in stuckTasks)
        {
            Logger.LogWarning("任务 {TaskId} 在 {Status} 超时（>{Timeout}），看门狗标记失败", task.Id, task.Status, _options.TaskTimeout);
            foreach (var pair in task.GetPairs().Where(p => p.Status == ComparePairStatus.Processing))
            {
                task.MarkPairFailed(pair.PairId, now, "比对超时（看门狗自动标记）");
            }
            task.MarkFailed($"比对/分析超时（看门狗自动标记，超时阈值 {_options.TaskTimeout.TotalMinutes} 分钟）");
            await taskRepository.UpdateAsync(task, autoSave: true);
        }
    }
}
