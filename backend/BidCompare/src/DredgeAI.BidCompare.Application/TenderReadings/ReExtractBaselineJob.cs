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
/// 重抽基准库后台任务：全量重抽最坏数分钟（多抽取器串行 + LLM 重试），
/// 不能在 HTTP 请求内同步执行（反代/网关必先超时）。
/// 异常就地落定失败态，不抛给 ABP 重试（LLM 按次计费）。
/// </summary>
public class ReExtractBaselineJob : AsyncBackgroundJob<ReExtractBaselineArgs>, ITransientDependency
{
    private readonly BaselineExtractionService _extractionService;
    private readonly IRepository<TenderReadingTask, Guid> _taskRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ReExtractBaselineJob(
        BaselineExtractionService extractionService,
        IRepository<TenderReadingTask, Guid> taskRepository,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _extractionService = extractionService;
        _taskRepository = taskRepository;
        _unitOfWorkManager = unitOfWorkManager;
    }

    [UnitOfWork]
    public override async Task ExecuteAsync(ReExtractBaselineArgs args)
    {
        try
        {
            if (args.Category.HasValue)
            {
                await _extractionService.ReExtractCategoryAsync(
                    args.TaskId, args.DocumentId, args.Category.Value, CancellationToken.None);
            }
            else
            {
                await _extractionService.ExecuteAsync(args.TaskId, args.DocumentId, CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "读标任务 {TaskId} 重抽基准库失败", args.TaskId);
            await TryMarkTaskFailedAsync(args.TaskId, $"基准库重抽失败：{ex.Message}");
        }
    }

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
