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

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// 比对后台任务（spec §5 步骤4）：汇总已解析标书 AnGIneer 原始产物 → 调算法服务三个端点 → 证据落库。
/// 算法服务为「一次传入全部标书、内部两两 + 多文档分析」设计，因此三个端点各调用一次（共 3 次），
/// 不按对拆请求（避免 N×(N-1)/2 倍请求与原始产物重复传输）。
/// 算法侧暂无逐对事件，pairs 在批处理返回后统一落定（spec §4.4：不伪造逐对进度）；
/// 算法服务不可用 → 全部对标记 Failed、任务 Failed（spec §9：不静默降级）。
/// 完成后 MarkAnalyzing + 入队 AiAnalysisJob。
/// </summary>
public class CompareDocumentsJob : AsyncBackgroundJob<CompareDocumentsArgs>, ITransientDependency
{
    private readonly IRepository<CompareTask, Guid> _taskRepository;
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IRepository<EvidenceItem, Guid> _evidenceRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ICompareAlgoClient _algoClient;
    private readonly IGuidGenerator _guidGenerator;
    private readonly IBackgroundJobManager _backgroundJobManager;

    public CompareDocumentsJob(
        IRepository<CompareTask, Guid> taskRepository,
        IRepository<CompareDocument, Guid> documentRepository,
        IRepository<EvidenceItem, Guid> evidenceRepository,
        IFileStorage fileStorage,
        ICompareAlgoClient algoClient,
        IGuidGenerator guidGenerator,
        IBackgroundJobManager backgroundJobManager)
    {
        _taskRepository = taskRepository;
        _documentRepository = documentRepository;
        _evidenceRepository = evidenceRepository;
        _fileStorage = fileStorage;
        _algoClient = algoClient;
        _guidGenerator = guidGenerator;
        _backgroundJobManager = backgroundJobManager;
    }

    public override async Task ExecuteAsync(CompareDocumentsArgs args)
    {
        var cancellationToken = CancellationToken.None;
        var task = await _taskRepository.GetAsync(args.TaskId, cancellationToken: cancellationToken);

        var bidDocs = (await _documentRepository.GetListAsync(d =>
                d.TaskId == args.TaskId &&
                d.Role == DocumentRole.Bid &&
                d.ParseStatus == DocumentParseStatus.Parsed,
                cancellationToken: cancellationToken))
            .OrderBy(d => d.CreationTime)
            .ToList();

        if (bidDocs.Count < 2)
        {
            task.MarkFailed($"可比对标书不足 2 份（当前 {bidDocs.Count} 份）");
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        var rawByDocId = new Dictionary<Guid, AlgoRawDocument>();
        foreach (var doc in bidDocs)
        {
            var rawGraphKey = doc.IrStorageKey!.Replace("/ir.json", "/raw/doc_blocks_graph.jsonl", System.StringComparison.Ordinal);
            var rawMetaKey = doc.IrStorageKey.Replace("/ir.json", "/raw/doc_blocks_graph_meta.json", System.StringComparison.Ordinal);

            await using var graphStream = await _fileStorage.GetAsync(rawGraphKey, cancellationToken);
            var graphJsonl = await ReadAllAsync(graphStream, cancellationToken);
            await using var metaStream = await _fileStorage.GetAsync(rawMetaKey, cancellationToken);
            var metaJson = await ReadAllAsync(metaStream, cancellationToken);

            rawByDocId[doc.Id] = new AlgoRawDocument(doc.Id.ToString(), graphJsonl, metaJson);
        }

        List<ComparePairItem> pairs;
        if (args.PairIds is { Count: > 0 })
        {
            pairs = task.GetPairs().Where(p => args.PairIds.Contains(p.PairId)).ToList();
            if (pairs.Count == 0)
            {
                task.MarkFailed("指定的比对对不存在");
                await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
                return;
            }
            task.ResetPairsForRetry(args.PairIds);
        }
        else
        {
            var combos = new List<(Guid DocA, Guid DocB)>();
            for (var i = 0; i < bidDocs.Count; i++)
            {
                for (var j = i + 1; j < bidDocs.Count; j++)
                {
                    combos.Add((bidDocs[i].Id, bidDocs[j].Id));
                }
            }
            task.InitializePairs(combos);
            pairs = task.GetPairs();
        }
        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);

        task.MarkPairBatchStarted(pairs.Select(p => p.PairId), DateTime.Now);
        task.UpdateProgress("comparing", 60, "算法分析中");
        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);

        List<AlgoEvidence> algoEvidences;
        try
        {
            var allDocs = rawByDocId.Values.ToList();
            algoEvidences = (await _algoClient.AnalyzeSimilarityAsync(task.Id.ToString(), allDocs, cancellationToken))
                .Concat(await _algoClient.AnalyzePricingAsync(task.Id.ToString(), allDocs, cancellationToken))
                .Concat(await _algoClient.AnalyzeMetadataAsync(task.Id.ToString(), allDocs, cancellationToken))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "算法服务调用失败，任务 {TaskId} 标记 Failed", args.TaskId);
            foreach (var pair in pairs)
            {
                task.MarkPairFailed(pair.PairId, DateTime.Now, ex.Message);
            }
            task.MarkFailed($"算法服务不可用：{ex.Message}");
            await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
            return;
        }

        // 重跑指定对时只吸收恰好涉及这些对的两份文档证据；全量重跑吸收全部（含 ≥3 份多文档证据）
        var retriedDocPairs = new HashSet<(string DocA, string DocB)>();
        if (args.PairIds is { Count: > 0 })
        {
            foreach (var pair in pairs)
            {
                retriedDocPairs.Add((pair.DocAId.ToString(), pair.DocBId.ToString()));
            }
        }

        foreach (var algoEvidence in algoEvidences)
        {
            if (retriedDocPairs.Count > 0)
            {
                if (algoEvidence.DocIds.Count != 2) continue;
                var (a, b) = (algoEvidence.DocIds[0], algoEvidence.DocIds[1]);
                if (!retriedDocPairs.Contains((a, b)) && !retriedDocPairs.Contains((b, a))) continue;
            }
            await _evidenceRepository.InsertAsync(
                EvidenceMapper.ToEntity(_guidGenerator.Create(), args.TaskId, algoEvidence),
                cancellationToken: cancellationToken);
        }

        // 批处理返回后逐对落定（本地快速处理，不伪造算法运行中的逐对进度）
        foreach (var pair in pairs)
        {
            task.MarkPairDone(pair.PairId, DateTime.Now, ReadPairSimilarity(algoEvidences, pair.DocAId, pair.DocBId));
        }
        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);

        task.MarkAnalyzing();
        task.UpdateProgress("analyzing", 80, "AI 分析中");
        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
        await _backgroundJobManager.EnqueueAsync(new AiAnalysisArgs { TaskId = args.TaskId });
    }

    private static double? ReadPairSimilarity(IReadOnlyList<AlgoEvidence> evidences, Guid docA, Guid docB)
    {
        var docAId = docA.ToString();
        var docBId = docB.ToString();
        foreach (var evidence in evidences)
        {
            if (evidence.DocIds.Count != 2 ||
                !evidence.DocIds.Contains(docAId) ||
                !evidence.DocIds.Contains(docBId))
            {
                continue;
            }
            if (evidence.Metrics != null &&
                evidence.Metrics.TryGetValue("similarity", out var element) &&
                element.ValueKind == System.Text.Json.JsonValueKind.Number)
            {
                return element.GetDouble();
            }
        }
        return null;
    }

    private static async Task<string> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
