using System;
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
/// 单文档解析后台任务（重试失败文档用）：下载原始文件 → 提交 AnGIneer → 轮询 → 下载产物包 →
/// AnGineerIrMapper 映射为内部适配 IR（v2 §2/§3）→ IR 校验（不合格拒收并报原因）→
/// 产物落对象存储（原始产物留档 raw/ + ir.json + content.md + images/）→ 更新文档与任务状态。
/// 首次解析改用 ParseDocumentsJob 批量并发；此处保留按文档重试。
/// </summary>
public class ParseDocumentJob : AsyncBackgroundJob<ParseDocumentArgs>, ITransientDependency
{
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IRepository<CompareTask, Guid> _taskRepository;
    private readonly DocumentParsePipeline _pipeline;
    private readonly ParseTaskStateAdvancer _advancer;

    public ParseDocumentJob(
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

    public override async Task ExecuteAsync(ParseDocumentArgs args)
    {
        var cancellationToken = CancellationToken.None;
        var document = await _documentRepository.FindAsync(args.DocumentId, cancellationToken: cancellationToken);
        if (document == null)
        {
            Logger.LogWarning("CompareDocument {DocumentId} 不存在，跳过解析", args.DocumentId);
            return;
        }
        // 任务已删除时不无限重试，直接结束
        var task = await _taskRepository.FindAsync(args.TaskId, cancellationToken: cancellationToken);
        if (task == null)
        {
            Logger.LogWarning("CompareTask {TaskId} 不存在，跳过解析（文档 {DocumentId}）", args.TaskId, args.DocumentId);
            return;
        }

        // 幂等：已解析成功/任务已推进到比对阶段 → 跳过，避免重试时重复提交 AnGIneer
        if (document.ParseStatus == DocumentParseStatus.Parsed)
        {
            Logger.LogInformation("文档 {DocumentId} 已解析，跳过重复任务", args.DocumentId);
            return;
        }
        if (task.Status is CompareTaskStatus.Comparing or CompareTaskStatus.Analyzing or CompareTaskStatus.Done)
        {
            Logger.LogWarning("任务 {TaskId} 已处于 {Status}，跳过过期解析任务（文档 {DocumentId}）",
                args.TaskId, task.Status, args.DocumentId);
            return;
        }

        try
        {
            await _pipeline.MarkParsingAsync(document, cancellationToken);

            var anGineerJobId = await _pipeline.GetOrResumeJobAsync(document, null, cancellationToken);
            var status = await _pipeline.PollUntilFinishedAsync(
                anGineerJobId, document, writeGate: null, cancellationToken);
            if (status.State == AnGineerJobState.Failed)
            {
                throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                    .WithData("fileName", document.FileName);
            }
            if (status.State == AnGineerJobState.Partial)
            {
                Logger.LogWarning(
                    "文档 {DocumentId} AnGIneer 返回 partial（soft 阶段失败），尝试下载核心产物: {StageMessage}",
                    args.DocumentId, status.StageMessage);
            }

            await _pipeline.CompleteAsync(document, anGineerJobId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "文档 {DocumentId} 解析失败", args.DocumentId);
            try
            {
                // 落失败态独立保护：此处再抛会跳过 finally 的状态推进，任务将永久卡在 Parsing
                await _pipeline.MarkFailedAsync(document, ex, cancellationToken);
            }
            catch (Exception markEx)
            {
                Logger.LogError(markEx, "文档 {DocumentId} 失败状态落库失败", args.DocumentId);
            }
        }
        finally
        {
            await _advancer.AdvanceAsync(task, cancellationToken);
        }
    }
}
