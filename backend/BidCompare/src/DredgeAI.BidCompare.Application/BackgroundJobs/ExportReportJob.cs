using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.Reporting;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>导出后台任务（spec §6.2 异步化）：报告 JSON → docx →（可选）pdf → 对象存储。</summary>
public class ExportReportJob : AsyncBackgroundJob<ExportReportArgs>, ITransientDependency
{
    private readonly IRepository<ExportJob, Guid> _exportJobRepository;
    private readonly IRepository<CompareTask, Guid> _taskRepository;
    private readonly ReportBuilder _reportBuilder;
    private readonly IWordReportRenderer _wordReportRenderer;
    private readonly IPdfConverter _pdfConverter;
    private readonly IFileStorage _fileStorage;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public ExportReportJob(
        IRepository<ExportJob, Guid> exportJobRepository,
        IRepository<CompareTask, Guid> taskRepository,
        ReportBuilder reportBuilder,
        IWordReportRenderer wordReportRenderer,
        IPdfConverter pdfConverter,
        IFileStorage fileStorage,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _exportJobRepository = exportJobRepository;
        _taskRepository = taskRepository;
        _reportBuilder = reportBuilder;
        _wordReportRenderer = wordReportRenderer;
        _pdfConverter = pdfConverter;
        _fileStorage = fileStorage;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// 显式提供环境 UoW：Job 执行器不保证 ambient UoW，报告构建中的仓储扩展方法
    /// （如 CountAsync(predicate)）会拿到已释放 DbContext 的 IQueryable 而抛 disposed 异常。
    /// </summary>
    public override async Task ExecuteAsync(ExportReportArgs args)
    {
        using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);
        try
        {
            await ExecuteCoreAsync(args, CancellationToken.None);
            await uow.CompleteAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 完整堆栈落日志便于排查；失败态由 ExecuteCoreAsync 内部落库
            Logger.LogWarning(ex, "导出任务 {ExportJobId} 失败", args.ExportJobId);
        }
    }

    private async Task ExecuteCoreAsync(ExportReportArgs args, CancellationToken cancellationToken)
    {
        var job = await _exportJobRepository.FindAsync(args.ExportJobId, cancellationToken: cancellationToken);
        if (job == null)
        {
            Logger.LogWarning("ExportJob {ExportJobId} 不存在，跳过导出", args.ExportJobId);
            return;
        }

        job.MarkRunning();
        await _exportJobRepository.UpdateAsync(job, autoSave: true, cancellationToken: cancellationToken);

        try
        {
            var task = await _taskRepository.GetAsync(job.TaskId, cancellationToken: cancellationToken);
            var report = await _reportBuilder.BuildAsync(job.TaskId, cancellationToken);
            var docx = await _wordReportRenderer.RenderAsync(report, task.Name, cancellationToken);

            byte[] output;
            string extension;
            string contentType;
            if (job.Format == ExportFormat.Word)
            {
                output = docx;
                extension = "docx";
                contentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
            }
            else
            {
                output = await _pdfConverter.ConvertToPdfAsync(docx, cancellationToken);
                extension = "pdf";
                contentType = "application/pdf";
            }

            var key = $"compare/{job.TaskId}/exports/{job.Id}.{extension}";
            await _fileStorage.UploadAsync(key, new MemoryStream(output), contentType, cancellationToken);
            job.MarkSucceeded(key);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "导出任务 {ExportJobId} 失败", args.ExportJobId);
            job.MarkFailed(ex.Message); // spec §9：导出失败可重试（重新 POST export 即可）
        }

        await _exportJobRepository.UpdateAsync(job, autoSave: true, cancellationToken: cancellationToken);
    }
}
