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
        var task = await _taskRepository.GetAsync(args.TaskId, cancellationToken: cancellationToken);

        try
        {
            await _pipeline.MarkParsingAsync(document, cancellationToken);

            var anGineerJobId = await _pipeline.SubmitAsync(document, cancellationToken);
            var state = await _pipeline.PollUntilFinishedAsync(anGineerJobId, cancellationToken);
            if (state == AnGineerJobState.Failed)
            {
                throw new BusinessException(BidCompareErrorCodes.AnGineerParseFailed)
                    .WithData("fileName", document.FileName);
            }

            await _pipeline.CompleteAsync(document, anGineerJobId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "文档 {DocumentId} 解析失败", args.DocumentId);
            await _pipeline.MarkFailedAsync(document, ex, cancellationToken);
        }

        await _advancer.AdvanceAsync(task, cancellationToken);
    }
}
