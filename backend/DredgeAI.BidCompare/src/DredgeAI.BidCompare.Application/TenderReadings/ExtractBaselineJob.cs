using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>
/// 抽取后台任务：解析完成后异步生成基准库。
/// LLM 抽取按次计费，异常不再抛给 ABP 重试（重试重复计费且任务滞留 Extracting），就地落定失败态。
/// </summary>
public class ExtractBaselineJob : AsyncBackgroundJob<ExtractBaselineArgs>, ITransientDependency
{
    private readonly BaselineExtractionService _extractionService;
    private readonly IRepository<TenderReadingTask, Guid> _taskRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ExtractBaselineJob(
        BaselineExtractionService extractionService,
        IRepository<TenderReadingTask, Guid> taskRepository,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _extractionService = extractionService;
        _taskRepository = taskRepository;
        _unitOfWorkManager = unitOfWorkManager;
    }

    [UnitOfWork]
    public override async Task ExecuteAsync(ExtractBaselineArgs args)
    {
        try
        {
            await _extractionService.ExecuteAsync(args.TaskId, args.DocumentId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "读标任务 {TaskId} 抽取基准库失败", args.TaskId);
            await TryMarkTaskFailedAsync(args.TaskId, $"基准库抽取失败：{ex.Message}");
        }
    }

    /// <summary>兜底标记失败：独立工作单元重读实体；再失败则交由看门狗任务超时兜底。</summary>
    private async Task TryMarkTaskFailedAsync(Guid taskId, string reason)
    {
        try
        {
            using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
            var task = await _taskRepository.FindAsync(taskId);
            if (task == null || task.Status is TenderReadingTaskStatus.Ready or TenderReadingTaskStatus.Failed)
            {
                return;
            }
            task.MarkFailed(reason);
            await _taskRepository.UpdateAsync(task, autoSave: true);
            await uow.CompleteAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "读标任务 {TaskId} 失败态落库失败，交由看门狗兜底", taskId);
        }
    }
}
