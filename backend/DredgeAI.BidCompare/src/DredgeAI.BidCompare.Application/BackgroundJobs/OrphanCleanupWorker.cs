using System;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Drafts;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Volo.Abp.Timing;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// 孤儿数据清扫：超时未转正的上传会话（草稿）与过期导出文件。
/// 草稿放弃上传后文件永久残留、导出文件永久保存，均按保留期清理。
/// </summary>
public class OrphanCleanupWorker : AsyncPeriodicBackgroundWorkerBase, ITransientDependency
{
    private readonly CleanupOptions _options;
    private readonly IClock _clock;

    public OrphanCleanupWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<CleanupOptions> options,
        IClock clock)
        : base(timer, serviceScopeFactory)
    {
        _options = options.Value;
        _clock = clock;
        Timer.Period = (int)_options.Period.TotalMilliseconds;
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        await SweepDraftsAsync(workerContext.ServiceProvider, _clock.Now);
        await SweepExportsAsync(workerContext.ServiceProvider, _clock.Now);
    }

    /// <summary>清理超时草稿：按会话前缀删存储 + 删行（internal 供测试以受控时间点直接驱动）。</summary>
    internal async Task SweepDraftsAsync(IServiceProvider serviceProvider, DateTime now)
    {
        var repository = serviceProvider.GetRequiredService<IRepository<CompareDraftDocument, Guid>>();
        var storage = serviceProvider.GetRequiredService<IFileStorage>();

        var deadline = now - _options.DraftRetention;
        var stale = await repository.GetListAsync(d => d.CreationTime < deadline);
        foreach (var group in stale.GroupBy(d => d.DraftId))
        {
            try
            {
                await storage.DeleteByPrefixAsync($"compare/drafts/{group.Key}/");
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "草稿会话 {DraftId} 存储清理失败，行仍会删除", group.Key);
            }
            await repository.DeleteManyAsync(group.ToList(), autoSave: true);
            Logger.LogInformation("清理超时草稿会话 {DraftId}（{Count} 份文档）", group.Key, group.Count());
        }
    }

    /// <summary>清理过期导出：删导出对象 + 任务句柄行（报告可由 ReportJson 重新生成）。</summary>
    internal async Task SweepExportsAsync(IServiceProvider serviceProvider, DateTime now)
    {
        var repository = serviceProvider.GetRequiredService<IRepository<ExportJob, Guid>>();
        var storage = serviceProvider.GetRequiredService<IFileStorage>();

        var deadline = now - _options.ExportRetention;
        var stale = await repository.GetListAsync(j => j.CreationTime < deadline);
        foreach (var job in stale)
        {
            if (job.FileStorageKey != null)
            {
                try
                {
                    await storage.DeleteAsync(job.FileStorageKey);
                }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "导出文件 {Key} 删除失败，行仍会删除", job.FileStorageKey);
                }
            }
            await repository.DeleteAsync(job, autoSave: true);
        }
        if (stale.Count > 0)
        {
            Logger.LogInformation("清理过期导出任务 {Count} 个", stale.Count);
        }
    }
}
