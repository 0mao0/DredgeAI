using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.Documents;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>读标文档解析后台任务：原始文件 → AnGIneer → 内部 IR → 对象存储 → 触发抽取。</summary>
public class ParseTenderDocumentJob : AsyncBackgroundJob<ParseTenderDocumentArgs>, ITransientDependency
{
    private readonly IRepository<TenderReadingDocument, Guid> _documentRepository;
    private readonly IRepository<TenderReadingTask, Guid> _taskRepository;
    private readonly TenderDocumentParsePipeline _pipeline;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public ParseTenderDocumentJob(
        IRepository<TenderReadingDocument, Guid> documentRepository,
        IRepository<TenderReadingTask, Guid> taskRepository,
        TenderDocumentParsePipeline pipeline,
        IBackgroundJobManager backgroundJobManager)
    {
        _documentRepository = documentRepository;
        _taskRepository = taskRepository;
        _pipeline = pipeline;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(ParseTenderDocumentArgs args)
    {
        var cancellationToken = CancellationToken.None;
        var document = await _documentRepository.FindAsync(args.DocumentId, cancellationToken: cancellationToken);
        if (document == null || document.TaskId != args.TaskId)
        {
            Logger.LogWarning("读标文档 {DocumentId} 不存在或不属于任务 {TaskId}，跳过解析", args.DocumentId, args.TaskId);
            return;
        }

        var task = await _taskRepository.FindAsync(args.TaskId, cancellationToken: cancellationToken);
        if (task == null)
        {
            Logger.LogWarning("读标任务 {TaskId} 不存在，跳过解析（文档 {DocumentId}）", args.TaskId, args.DocumentId);
            return;
        }

        if (document.ParseStatus == DocumentParseStatus.Parsed)
        {
            Logger.LogInformation("读标文档 {DocumentId} 已解析，跳过重复任务", args.DocumentId);
            return;
        }

        try
        {
            await _pipeline.MarkParsingAsync(document, cancellationToken);
            if (task.Status != TenderReadingTaskStatus.Parsing)
            {
                task.StartParsing();
                await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            }

            var anGineerJobId = await _pipeline.GetOrResumeJobAsync(document, null, cancellationToken);
            var status = await _pipeline.PollUntilFinishedAsync(anGineerJobId, document, null, cancellationToken);
            if (status.State == AnGineerJobState.Failed)
            {
                throw new BusinessException(TenderReadErrorCodes.AnGineerParseFailed)
                    .WithData("fileName", document.FileName)
                    .WithData("reason", status.FailureReason ?? "解析失败");
            }

            if (status.State == AnGineerJobState.Partial)
            {
                Logger.LogWarning(
                    "读标文档 {DocumentId} AnGIneer 返回 partial，尝试下载核心产物: {StageMessage}",
                    args.DocumentId, status.StageMessage);
            }

            await _pipeline.CompleteAsync(document, anGineerJobId, cancellationToken);

            task.MarkParsed();
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "读标文档 {DocumentId} 解析失败", args.DocumentId);
            try
            {
                await _pipeline.MarkFailedAsync(document, ex, cancellationToken);
                task.MarkFailed($"文档 {document.FileName} 解析失败：{ex.Message}");
                await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            }
            catch (Exception markEx)
            {
                Logger.LogError(markEx, "读标文档 {DocumentId} 失败状态落库失败", args.DocumentId);
            }
            return;
        }

        // 解析成功后触发抽取；入队失败不应把已解析文档/任务改判为失败
        try
        {
            // 多文档任务等全部文档落定后才触发一次全量抽取：
            // 逐份触发会并发执行「删旧重建」，字段与锚点互相覆盖错配
            // 注意：CountAsync(predicate) 是扩展方法，Job 无环境 UoW 时必挂，用 GetListAsync 内存计数
            var unsettled = await _documentRepository.GetListAsync(d =>
                d.TaskId == task.Id &&
                (d.ParseStatus == DocumentParseStatus.Pending || d.ParseStatus == DocumentParseStatus.Parsing),
                cancellationToken: cancellationToken);
            var remaining = unsettled.Count(d => d.Id != document.Id);
            if (remaining > 0)
            {
                Logger.LogInformation("读标任务 {TaskId} 仍有 {Count} 份文档解析中，抽取延后到全部落定", task.Id, remaining);
                return;
            }
            await _backgroundJobManager.EnqueueAsync(new ExtractBaselineArgs
            {
                TaskId = task.Id,
                DocumentId = document.Id
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "读标任务 {TaskId} 解析成功但抽取任务入队失败", args.TaskId);
        }
    }
}
