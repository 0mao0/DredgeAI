using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// 比对后台任务（spec §5 步骤4）：汇总已解析标书 IR → 调算法服务三个端点 → 证据落库。
/// 算法服务不可用 → 任务 Failed（spec §9：不静默降级）。
/// P1 版本比对完成即 Done；Task 12（P2）会把尾部改为 MarkAnalyzing + 入队 AiAnalysisJob。
/// </summary>
public class CompareDocumentsJob : AsyncBackgroundJob<CompareDocumentsArgs>, ITransientDependency
{
    private readonly IRepository<CompareTask, Guid> _taskRepository;
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IRepository<EvidenceItem, Guid> _evidenceRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ICompareAlgoClient _algoClient;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public CompareDocumentsJob(
        IRepository<CompareTask, Guid> taskRepository,
        IRepository<CompareDocument, Guid> documentRepository,
        IRepository<EvidenceItem, Guid> evidenceRepository,
        IFileStorage fileStorage,
        ICompareAlgoClient algoClient,
        IAsyncQueryableExecuter asyncExecuter,
        IGuidGenerator guidGenerator,
        IBackgroundJobManager backgroundJobManager)
    {
        _taskRepository = taskRepository;
        _documentRepository = documentRepository;
        _evidenceRepository = evidenceRepository;
        _fileStorage = fileStorage;
        _algoClient = algoClient;
        _asyncExecuter = asyncExecuter;
        _guidGenerator = guidGenerator;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(CompareDocumentsArgs args)
    {
        var cancellationToken = CancellationToken.None;
        var task = await _taskRepository.GetAsync(args.TaskId, cancellationToken: cancellationToken);

        var queryable = await _documentRepository.GetQueryableAsync();
        var bidDocs = await _asyncExecuter.ToListAsync(queryable.Where(d =>
            d.TaskId == args.TaskId &&
            d.Role == DocumentRole.Bid &&
            d.ParseStatus == DocumentParseStatus.Parsed));

        if (bidDocs.Count < 2)
        {
            task.MarkFailed($"可比对标书不足 2 份（当前 {bidDocs.Count} 份）");
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        var algoDocuments = new List<AlgoIrDocument>();
        foreach (var doc in bidDocs)
        {
            await using var irStream = await _fileStorage.GetAsync(doc.IrStorageKey!, cancellationToken);
            var irJson = await ReadAllAsync(irStream, cancellationToken);
            string? docMd = null;
            if (doc.DocMdStorageKey != null)
            {
                await using var mdStream = await _fileStorage.GetAsync(doc.DocMdStorageKey, cancellationToken);
                docMd = await ReadAllAsync(mdStream, cancellationToken);
            }
            algoDocuments.Add(new AlgoIrDocument(doc.Id.ToString(), irJson, docMd));
        }

        List<AlgoEvidence> algoEvidences;
        try
        {
            algoEvidences = (await _algoClient.AnalyzeSimilarityAsync(algoDocuments, cancellationToken))
                .Concat(await _algoClient.AnalyzePricingAsync(algoDocuments, cancellationToken))
                .Concat(await _algoClient.AnalyzeMetadataAsync(algoDocuments, cancellationToken))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "算法服务调用失败，任务 {TaskId} 标记 Failed", args.TaskId);
            task.MarkFailed($"算法服务不可用：{ex.Message}");
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        foreach (var algoEvidence in algoEvidences)
        {
            await _evidenceRepository.InsertAsync(
                EvidenceMapper.ToEntity(_guidGenerator.Create(), args.TaskId, algoEvidence),
                cancellationToken: cancellationToken);
        }

        task.MarkAnalyzing();
        task.UpdateProgress("analyzing", 80, "AI 分析中");
        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
        await _backgroundJobManager.EnqueueAsync(new AiAnalysisArgs { TaskId = args.TaskId });
    }

    private static async Task<string> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
