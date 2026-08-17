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

namespace DredgeAI.BidCompare.BackgroundJobs;

/// <summary>
/// AI 分析（spec §5 步骤4 后半段）：逐份标书条款响应判定 + 关键指标抽取。
/// spec §9：AI 失败/超时 → 算法证据照常展示，任务仍 Done，进度信息标注「AI 分析暂不可用」。
/// </summary>
public class AiAnalysisJob : AsyncBackgroundJob<AiAnalysisArgs>, ITransientDependency
{
    private const int DocMdMaxChars = 20000;      // 条款判定单份截断
    private const int IndicatorDocMaxChars = 8000; // 指标抽取单份 prompt 预算
    private const int IndicatorWindowBefore = 300;  // 关键词前取多少字
    private const int IndicatorWindowAfter = 700;   // 关键词后取多少字
    private const int IndicatorMaxWindowsPerKeyword = 3;

    private static readonly string[] IndicatorKeywords =
    {
        "投标总价", "报价", "工期", "资质", "质量目标",
        "技术方案", "施工方案", "售后服务", "项目业绩", "项目经理",
    };

    private const string ClauseJudgementSystemPrompt =
        "你是招投标评审助手。给定一条强制性条款与一份标书全文，判断该标书是否实质响应此条款。" +
        "只返回 JSON 数组，不要输出任何其他文字。";

    private const string IndicatorSystemPrompt =
        "你是招投标评审助手。请严格按固定指标清单从多份标书中抽取关键指标用于比选，清单如下：报价、工期、资质等级、质量目标、技术方案要点、售后服务、项目业绩、项目经理。" +
        "标书中没有的指标，summary 必须写“未提供/未明确”；禁止用文档章节标题代替指标，禁止输出清单之外的指标。" +
        "只返回 JSON 数组，每项字段：indicator（指标名）、summaries（数组，每项含 docId、summary）。不要输出任何其他文字。";

    private readonly IRepository<CompareTask, Guid> _taskRepository;
    private readonly IRepository<CompareDocument, Guid> _documentRepository;
    private readonly IRepository<EvidenceItem, Guid> _evidenceRepository;
    private readonly IFileStorage _fileStorage;
    private readonly ILlmGateway _llmGateway;
    private readonly IGuidGenerator _guidGenerator;

    public AiAnalysisJob(
        IRepository<CompareTask, Guid> taskRepository,
        IRepository<CompareDocument, Guid> documentRepository,
        IRepository<EvidenceItem, Guid> evidenceRepository,
        IFileStorage fileStorage,
        ILlmGateway llmGateway,
        IGuidGenerator guidGenerator)
    {
        _taskRepository = taskRepository;
        _documentRepository = documentRepository;
        _evidenceRepository = evidenceRepository;
        _fileStorage = fileStorage;
        _llmGateway = llmGateway;
        _guidGenerator = guidGenerator;
    }

    public override async Task ExecuteAsync(AiAnalysisArgs args)
    {
        var cancellationToken = CancellationToken.None;
        var task = await _taskRepository.GetAsync(args.TaskId, cancellationToken: cancellationToken);

        try
        {
            var bidDocs = await _documentRepository.GetListAsync(d =>
                d.TaskId == args.TaskId &&
                d.Role == DocumentRole.Bid &&
                d.ParseStatus == DocumentParseStatus.Parsed);

            // 重跑幂等：先清掉本任务旧的 AI 证据（条款判定 + 指标抽取整体重建），
            // 删除与后续插入在同一工作单元，重跑不再重复累积
            var staleAiEvidences = await _evidenceRepository.GetListAsync(
                e => e.TaskId == args.TaskId && e.AiGenerated, cancellationToken: cancellationToken);
            if (staleAiEvidences.Count > 0)
            {
                await _evidenceRepository.DeleteManyAsync(staleAiEvidences, autoSave: true, cancellationToken: cancellationToken);
            }

            var docMds = new Dictionary<CompareDocument, string>();
            foreach (var doc in bidDocs.Where(d => d.DocMdStorageKey != null))
            {
                // 流式限量读取：LLM prompt 只需前缀，整份 content.md 不再全量驻留后再截断
                await using var stream = await _fileStorage.GetAsync(doc.DocMdStorageKey!, cancellationToken);
                docMds[doc] = await ReadAllAsync(stream, cancellationToken);
            }

            var snapshot = task.ClauseSnapshotJson == null
                ? new List<ClauseSnapshotItem>()
                : JsonSerializer.Deserialize<List<ClauseSnapshotItem>>(
                    task.ClauseSnapshotJson, CompareTaskAppService.SnapshotJsonOptions) ?? new();

            if (snapshot.Count > 0)
            {
                task.UpdateProgress("analyzing", 80, "条款响应判定中");
                await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
                await RunClauseJudgementAsync(args.TaskId, snapshot, docMds, cancellationToken);
            }

            if (docMds.Count > 0)
            {
                task.UpdateProgress("analyzing", 88, "关键指标抽取中");
                await _taskRepository.UpdateAsync(task, autoSave: true, cancellationToken: cancellationToken);
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

        // v2 §5.3：仍有失败文档时以 partial 收尾（结果正常 + 失败文档内联重试），否则 Done
        var failedDocs = await _documentRepository.GetListAsync(d =>
            d.TaskId == args.TaskId && d.ParseStatus == DocumentParseStatus.Failed);
        if (failedDocs.Count > 0)
        {
            task.MarkPartial(string.Join("；", failedDocs.Select(f => $"{f.FileName}: {f.ParseError}")));
            var partialSuffix = $"{failedDocs.Count} 份文档解析失败，已跳过；其余结果不受影响";
            task.UpdateProgress("done", 100,
                task.ProgressMessage.IsNullOrWhiteSpace()
                    ? partialSuffix
                    : $"{task.ProgressMessage}；{partialSuffix}");
        }
        else
        {
            task.MarkDone();
        }
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
                var clauseMetricsJson = JsonSerializer.Serialize(new
                {
                    clauseId = judgement.ClauseId,
                    clauseText = clause?.Text ?? "",
                    status = judgement.Status
                }, CompareTaskAppService.SnapshotJsonOptions);

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
                    metricsJson: clauseMetricsJson,
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
            $"=== 标书 docId={kv.Key.Id}（{kv.Key.FileName}）===\n{SampleIndicatorText(kv.Value)}"));

        var userPrompt =
            docsSection +
            "\n\n请按固定指标清单抽取：报价、工期、资质等级、质量目标、技术方案要点、售后服务、项目业绩、项目经理；" +
            "缺失的指标 summary 填“未提供/未明确”，禁止用章节标题代替。以 JSON 数组返回，每项字段：indicator（指标名）、" +
            "summaries（数组，每项含 docId、summary）。只返回 JSON。";

        var response = await _llmGateway.CompleteAsync(IndicatorSystemPrompt, userPrompt, cancellationToken);

        foreach (var indicator in ParseIndicators(response))
        {
            var relatedDocIds = indicator.Summaries
                .Select(s => Guid.TryParse(s.DocId, out var id) ? id : Guid.Empty)
                .Where(id => id != Guid.Empty)
                .ToList();
            var indicatorMetricsJson = JsonSerializer.Serialize(new
            {
                summaries = indicator.Summaries.Select(s => new { s.DocId, s.Summary })
            }, CompareTaskAppService.SnapshotJsonOptions);

            await _evidenceRepository.InsertAsync(new EvidenceItem(
                _guidGenerator.Create(),
                taskId,
                EvidenceType.Indicator,
                EvidenceSeverity.Low,
                EvidenceMapper.SerializeDocIds(relatedDocIds),
                EvidenceMapper.SerializeLocations(Enumerable.Empty<EvidenceLocationDto>()),
                metricsJson: indicatorMetricsJson,
                title: $"指标比选：{indicator.Indicator}",
                description: string.Join("；", indicator.Summaries.Select(s => $"{s.DocId}: {s.Summary}")),
                aiGenerated: true), cancellationToken: cancellationToken);
        }
    }

    private static string Truncate(string text, int maxChars)
        => text.Length <= maxChars ? text : text[..maxChars] + "\n（截断）";

    /// <summary>限量读取文本流：最多 maxChars 字符即停，超长内容不再全量驻留内存。</summary>
    /// <summary>指标抽取长文档采样：按关键指标关键词定位并抽取上下文，避免遗漏中间章节。</summary>
    private static string SampleIndicatorText(string text)
    {
        if (text.Length <= IndicatorDocMaxChars)
        {
            return text;
        }

        var windows = new List<(int Start, int End, int Priority)>();
        for (var ki = 0; ki < IndicatorKeywords.Length; ki++)
        {
            var keyword = IndicatorKeywords[ki];
            var idx = text.IndexOf(keyword, StringComparison.Ordinal);
            var count = 0;
            while (idx >= 0 && count < IndicatorMaxWindowsPerKeyword)
            {
                windows.Add((
                    Math.Max(0, idx - IndicatorWindowBefore),
                    Math.Min(text.Length, idx + keyword.Length + IndicatorWindowAfter),
                    ki));
                count++;
                idx = text.IndexOf(keyword, idx + keyword.Length, StringComparison.Ordinal);
            }
        }

        if (windows.Count == 0)
        {
            return HeadTailSample(text);
        }

        var budget = IndicatorDocMaxChars;
        var selected = new List<(int Start, int End)>();
        foreach (var w in windows.OrderBy(w => w.Priority).ThenBy(w => w.Start))
        {
            if (budget <= 0)
            {
                break;
            }
            var length = w.End - w.Start;
            if (length > budget)
            {
                selected.Add((w.Start, w.Start + budget));
                budget = 0;
            }
            else
            {
                selected.Add((w.Start, w.End));
                budget -= length;
            }
        }

        var ordered = selected.OrderBy(w => w.Start).ToList();
        var merged = new List<(int Start, int End)>();
        foreach (var w in ordered)
        {
            if (merged.Count == 0 || w.Start > merged[^1].End)
            {
                merged.Add(w);
            }
            else
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, w.End));
            }
        }

        var builder = new System.Text.StringBuilder();
        foreach (var (start, end) in merged)
        {
            if (builder.Length >= IndicatorDocMaxChars)
            {
                break;
            }
            if (builder.Length > 0)
            {
                builder.Append("\n……\n");
            }
            var take = Math.Min(end - start, IndicatorDocMaxChars - builder.Length);
            builder.Append(text.Substring(start, take));
        }
        return builder.ToString();
    }

    private static string HeadTailSample(string text)
    {
        var half = IndicatorDocMaxChars / 2;
        var head = text[..Math.Min(half, text.Length)];
        var tailStart = Math.Max(half, text.Length - half);
        return $"{head}\n\n……（中间省略）……\n\n{text[tailStart..]}";
    }

    private static async Task<string> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static async Task<string> ReadLimitedAsync(Stream stream, int maxChars, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream);
        var buffer = new char[4096];
        var builder = new System.Text.StringBuilder();
        while (builder.Length < maxChars)
        {
            var read = await reader.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }
            builder.Append(buffer, 0, read); // 单块最多多读 4095 字符，最终统一截断
        }
        var text = builder.ToString();
        return text.Length <= maxChars ? text : text[..maxChars];
    }

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
