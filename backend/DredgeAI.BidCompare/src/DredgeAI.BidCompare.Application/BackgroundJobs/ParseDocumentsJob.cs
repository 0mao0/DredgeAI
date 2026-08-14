using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.AnGineer;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// 批量解析后台任务（v2 修订）：一次入队多份文档，并发提交 AnGIneer 并轮询；
/// AnGIneer 侧由 MINERU_MAX_CONCURRENCY 闸门限制同时解析份数。
/// DB/存储写入统一串行（EF Core DbContext 非线程安全），只有 HTTP 阶段并行。
/// </summary>
public class ParseDocumentsJob : AsyncBackgroundJob<ParseDocumentsArgs>, ITransientDependency
{
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IRepository<CompareTask, Guid> _taskRepository;
    private readonly DocumentParsePipeline _pipeline;
    private readonly ParseTaskStateAdvancer _advancer;

    public ParseDocumentsJob(
        IRepository<CompareDocument, Guid> documentRepository,
        IRepository<CompareTask, Guid> taskRepository,
        DocumentParsePipeline pipeline,
        ParseTaskStateAdvancer advancer)
    {
        _documentRepository = documentRepository;
        _taskRepository = taskRepository;
        _pipeline = pipeline;
        _advancer = advancer;
    }

    public override async Task ExecuteAsync(ParseDocumentsArgs args)
    {
        var cancellationToken = CancellationToken.None;
        var documents = await _documentRepository.GetListAsync(
            d => d.TaskId == args.TaskId && args.DocumentIds.Contains(d.Id),
            cancellationToken: cancellationToken);
        if (documents.Count == 0)
        {
            return;
        }
        var task = await _taskRepository.GetAsync(args.TaskId, cancellationToken: cancellationToken);

        using var writeGate = new SemaphoreSlim(1, 1);
        await Task.WhenAll(documents.Select(async document =>
        {
            try
            {
                await writeGate.WaitAsync(cancellationToken);
                try
                {
                    await _pipeline.MarkParsingAsync(document, cancellationToken);
                }
                finally
                {
                    writeGate.Release();
                }

                var jobId = await _pipeline.SubmitAsync(document, cancellationToken);
                var state = await _pipeline.PollUntilFinishedAsync(jobId, cancellationToken);
                if (state == AnGineerJobState.Failed)
                {
                    throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                        .WithData("fileName", document.FileName);
                }

                await writeGate.WaitAsync(cancellationToken);
                try
                {
                    await _pipeline.CompleteAsync(document, jobId, cancellationToken);
                }
                finally
                {
                    writeGate.Release();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Logger.LogWarning(ex, "文档 {DocumentId} 解析失败", document.Id);
                await writeGate.WaitAsync(cancellationToken);
                try
                {
                    await _pipeline.MarkFailedAsync(document, ex, cancellationToken);
                }
                finally
                {
                    writeGate.Release();
                }
            }
        }));

        await _advancer.AdvanceAsync(task, cancellationToken);
    }
}
