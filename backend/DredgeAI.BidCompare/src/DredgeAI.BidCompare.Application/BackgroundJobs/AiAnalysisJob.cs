using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Storage;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Linq;

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// AI 分析（spec §5 步骤4 后半段）：逐份标书条款响应判定 + 关键指标抽取。
/// spec §9：AI 失败/超时 → 算法证据照常展示，任务仍 Done，进度信息标注「AI 分析暂不可用」。
/// </summary>
public class AiAnalysisJob : AsyncBackgroundJob<AiAnalysisArgs>, ITransientDependency
{
    private const int DocMdMaxChars = 20000;      // 条款判定单份截断
    private const int IndicatorDocMaxChars = 8000; // 指标抽取单份截断

    private const string ClauseJudgementSystemPrompt =
        "你是招投标评审助手。给定一条强制性条款与一份标书全文，判断该标书是否实质响应此条款。" +
        "只返回 JSON 数组，不要输出任何其他文字。";

    private const string IndicatorSystemPrompt =
        "你是招投标评审助手。从多份标书中抽取关键指标（报价、工期、资质、技术方案要点等）用于比选。" +
        "只返回 JSON 数组，不要输出任何其他文字。";

    private readonly IRepository<CompareTask, Guid> _taskRepository;
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IRepository<EvidenceItem, Guid> _evidenceRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ILlmGateway _llmGateway;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IGuidGenerator _guidGenerator;

    public AiAnalysisJob(
        IRepository<CompareTask, Guid> taskRepository,
        IRepository<CompareDocument, Guid> documentRepository,
        IRepository<EvidenceItem, Guid> evidenceRepository,
        IFileStorage fileStorage,
        ILlmGateway llmGateway,
        IAsyncQueryableExecuter asyncExecuter,
        IGuidGenerator guidGenerator)
    {
        _taskRepository = taskRepository;
        _documentRepository = documentRepository;
        _evidenceRepository = evidenceRepository;
        _fileStorage = fileStorage;
        _llmGateway = llmGateway;
        _asyncExecuter = asyncExecuter;
        _guidGenerator = guidGenerator;
    }

    public override async Task ExecuteAsync(AiAnalysisArgs args)
    {
        var cancellationToken = CancellationToken.None;
        var task = await _taskRepository.GetAsync(args.TaskId, cancellationToken: cancellationToken);

        try
        {
            var queryable = await _documentRepository.GetQueryableAsync();
            var bidDocs = await _asyncExecuter.ToListAsync(queryable.Where(d =>
                d.TaskId == args.TaskId &&
                d.Role == DocumentRole.Bid &&
                d.ParseStatus == DocumentParseStatus.Parsed));

            var docMds = new Dictionary<CompareDocument, string>();
            foreach (var doc in bidDocs.Where(d => d.DocMdStorageKey != null))
            {
                await using var stream = await _fileStorage.GetAsync(doc.DocMdStorageKey!, cancellationToken);
                using var reader = new StreamReader(stream);
                docMds[doc] = await reader.ReadToEndAsync(cancellationToken);
            }

            var snapshot = task.ClauseSnapshotJson == null
                ? new List<ClauseSnapshotItem>()
                : JsonSerializer.Deserialize<List<ClauseSnapshotItem>>(
                    task.ClauseSnapshotJson, CompareTaskAppService.SnapshotJsonOptions) ?? new();

            if (snapshot.Count > 0)
            {
                await RunClauseJudgementAsync(args.TaskId, snapshot, docMds, cancellationToken);
            }

            if (docMds.Count > 0)
            {
                await RunIndicatorExtractionAsync(args.TaskId, docMds, cancellationToken);
            }

            task.UpdateProgress("done", 100, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // spec §9：AI 区块显示「AI 分析暂不可用」，不阻塞整体
            Logger.LogWarning(ex, "任务 {TaskId} AI 分析失败，降级为仅算法证据", args.TaskId);
            task.UpdateProgress("done", 100, "AI 分析暂不可用，可重新触发条款确认以重试");
        }

        task.MarkDone();
        await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
    }

    private async Task RunClauseJudgementAsync(
        Guid taskId,
        List<ClauseSnapshotItem> snapshot,
        Dictionary<CompareDocument, string> docMds,
        CancellationToken cancellationToken)
    {
        var clausesJson = JsonSerializer.Serialize(
            snapshot.Select(c => new { c.ClauseId, c.Text, c.Mandatory }),
            CompareTaskAppService.SnapshotJsonOptions);

        foreach (var (doc, docMd) in docMds)
        {
            var userPrompt =
                "强制性条款清单（JSON）：\n" + clausesJson +
                "\n\n标书全文（Markdown，可能截断）：\n" + Truncate(docMd, DocMdMaxChars) +
                "\n\n请逐条判定，以 JSON 数组返回，每项字段：clauseId、status（responded=实质响应 / partial=部分响应 / none=未响应）、reason（判定理由）、blockIds（相关原文块 id 数组，可为空）。只返回 JSON。";

            var response = await _llmGateway.CompleteAsync(ClauseJudgementSystemPrompt, userPrompt, cancellationToken);

            foreach (var judgement in ParseJudgements(response))
            {
                if (judgement.Status == "responded")
                {
                    continue; // 响应正常不产证据
                }
                var clause = snapshot.FirstOrDefault(c => c.ClauseId == judgement.ClauseId);
                var mandatory = clause?.Mandatory ?? true;
                var severity = (mandatory, judgement.Status) switch
                {
                    (true, "none") => EvidenceSeverity.High,
                    (true, "partial") => EvidenceSeverity.Mid,
                    (false, "none") => EvidenceSeverity.Mid,
                    _ => EvidenceSeverity.Low
                };

                await _evidenceRepository.InsertAsync(new EvidenceItem(
                    _guidGenerator.Create(),
                    taskId,
                    EvidenceType.Clause,
                    severity,
                    EvidenceMapper.SerializeDocIds(new[] { doc.Id }),
                    EvidenceMapper.SerializeLocations(new[]
                    {
                        new EvidenceLocationDto { DocId = doc.Id, BlockIds = judgement.BlockIds }
                    }),
                    metricsJson: null,
                    title: $"条款未实质响应（{doc.FileName}）：{clause?.Text ?? judgement.ClauseId}",
                    description: judgement.Reason,
                    aiGenerated: true), cancellationToken: cancellationToken);
            }
        }
    }

    private async Task RunIndicatorExtractionAsync(
        Guid taskId,
        Dictionary<CompareDocument, string> docMds,
        CancellationToken cancellationToken)
    {
        var docsSection = string.Join("\n\n", docMds.Select(kv =>
            $"=== 标书 docId={kv.Key.Id}（{kv.Key.FileName}）===\n{Truncate(kv.Value, IndicatorDocMaxChars)}"));

        var userPrompt =
            docsSection +
            "\n\n请抽取关键指标，以 JSON 数组返回，每项字段：indicator（指标名）、summaries（数组，每项含 docId、summary）。只返回 JSON。";

        var response = await _llmGateway.CompleteAsync(IndicatorSystemPrompt, userPrompt, cancellationToken);

        foreach (var indicator in ParseIndicators(response))
        {
            var relatedDocIds = indicator.Summaries
                .Select(s => Guid.TryParse(s.DocId, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();

            await _evidenceRepository.InsertAsync(new EvidenceItem(
                _guidGenerator.Create(),
                taskId,
                EvidenceType.Indicator,
                EvidenceSeverity.Low,
                EvidenceMapper.SerializeDocIds(relatedDocIds),
                EvidenceMapper.SerializeLocations(Enumerable.Empty<EvidenceLocationDto>()),
                metricsJson: null,
                title: $"指标比选：{indicator.Indicator}",
                description: string.Join("；", indicator.Summaries.Select(s => $"{s.DocId}: {s.Summary}")),
                aiGenerated: true), cancellationToken: cancellationToken);
        }
    }

    private static string Truncate(string text, int maxChars)
        => text.Length <= maxChars ? text : text[..maxChars] + "\n（截断）";

    private static List<ClauseJudgement> ParseJudgements(string llmResponse)
    {
        try
        {
            using var document = JsonDocument.Parse(StripFence(llmResponse));
            var result = new List<ClauseJudgement>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                result.Add(new ClauseJudgement(
                    element.TryGetProperty("clauseId", out var c) ? c.GetString() ?? "" : "",
                    element.TryGetProperty("status", out var s) ? s.GetString()?.ToLowerInvariant() ?? "none" : "none",
                    element.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "",
                    element.TryGetProperty("blockIds", out var b) && b.ValueKind == JsonValueKind.Array
                        ? b.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x != "").ToList()
                        : new List<string>()));
            }
            return result;
        }
        catch (JsonException ex)
        {
            throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                .WithData("reason", $"LLM 条款判定响应不是合法 JSON：{ex.Message}");
        }
    }

    private static List<IndicatorItem> ParseIndicators(string llmResponse)
    {
        try
        {
            using var document = JsonDocument.Parse(StripFence(llmResponse));
            var result = new List<IndicatorItem>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var summaries = new List<IndicatorSummary>();
                if (element.TryGetProperty("summaries", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var s in arr.EnumerateArray())
                    {
                        summaries.Add(new IndicatorSummary(
                            s.TryGetProperty("docId", out var d) ? d.GetString() ?? "" : "",
                            s.TryGetProperty("summary", out var m) ? m.GetString() ?? "" : ""));
                    }
                }
                result.Add(new IndicatorItem(
                    element.TryGetProperty("indicator", out var i) ? i.GetString() ?? "未命名指标" : "未命名指标",
                    summaries));
            }
            return result;
        }
        catch (JsonException ex)
        {
            throw new BusinessException(BidCompareErrorCodes.IrValidationFailed)
                .WithData("reason", $"LLM 指标抽取响应不是合法 JSON：{ex.Message}");
        }
    }

    private static string StripFence(string text)
    {
        var json = text.Trim();
        if (json.StartsWith("```"))
        {
            var firstNewline = json.IndexOf('\n');
            var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewline > 0 && lastFence > firstNewline)
            {
                json = json[(firstNewline + 1)..lastFence].Trim();
            }
        }
        return json;
    }

    private record ClauseJudgement(string ClauseId, string Status, string Reason, List<string> BlockIds);

    private record IndicatorItem(string Indicator, List<IndicatorSummary> Summaries);

    private record IndicatorSummary(string DocId, string Summary);
}
