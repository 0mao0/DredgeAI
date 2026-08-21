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
        var task = await _taskRepository.FindAsync(args.TaskId, cancellationToken: cancellationToken);
        if (task == null)
        {
            Logger.LogWarning("CompareTask {TaskId} 不存在，跳过批量解析", args.TaskId);
            return;
        }
        // 幂等：任务已推进到比对阶段 → 过期批量任务直接结束，不重复提交 AnGIneer
        if (task.Status is CompareTaskStatus.Comparing or CompareTaskStatus.Analyzing or CompareTaskStatus.Done)
        {
            Logger.LogWarning("任务 {TaskId} 已处于 {Status}，跳过过期批量解析任务", args.TaskId, task.Status);
            return;
        }

        var documents = (await _documentRepository.GetListAsync(
                d => d.TaskId == args.TaskId && args.DocumentIds.Contains(d.Id),
                cancellationToken: cancellationToken))
            // 幂等：已解析文档跳过（重复入队/重试场景）
            .Where(d => d.ParseStatus != DocumentParseStatus.Parsed)
            .ToList();
        if (documents.Count == 0)
        {
            return;
        }

        try
        {
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

                    var jobId = await _pipeline.GetOrResumeJobAsync(document, writeGate, cancellationToken);
                    var status = await _pipeline.PollUntilFinishedAsync(
                        jobId, document, writeGate, cancellationToken);
                    if (status.State == AnGineerJobState.Failed)
                    {
                        throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                            .WithData("fileName", document.FileName)
                            .WithData("reason", status.FailureReason ?? "解析失败");
                    }
                    if (status.State == AnGineerJobState.Partial)
                    {
                        Logger.LogWarning(
                            "文档 {DocumentId} AnGIneer 返回 partial（soft 阶段失败），尝试下载核心产物: {StageMessage}",
                            document.Id, status.StageMessage);
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
                        // 落失败态独立保护：此处再抛会跳过 finally 的状态推进，任务将永久卡在 Parsing
                        await _pipeline.MarkFailedAsync(document, ex, cancellationToken);
                    }
                    catch (Exception markEx)
                    {
                        Logger.LogError(markEx, "文档 {DocumentId} 失败状态落库失败", document.Id);
                    }
                    finally
                    {
                        writeGate.Release();
                    }
                }
            }));
        }
        finally
        {
            await _advancer.AdvanceAsync(args.TaskId, cancellationToken);
        }
    }
}
