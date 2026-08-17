using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.CompareTasks;

public class CompareTask : FullAuditedAggregateRoot<Guid>
{
    internal static readonly JsonSerializerOptions PairJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public string Name { get; private set; } = default!;

    public CompareTaskStatus Status { get; private set; }

    public Guid? TenderDocumentId { get; private set; }

    /// <summary>条款清单快照（JSON 数组，元素见 ClauseSnapshotItem），锁定后不可变（spec §6.2）。</summary>
    public string? ClauseSnapshotJson { get; private set; }

    /// <summary>报告 JSON 缓存（CompareReportDto 序列化），任务 Done 后生成。</summary>
    public string? ReportJson { get; private set; }

    public DateTime? ReportGeneratedAt { get; private set; }

    public string ProgressStage { get; private set; } = "parsing";

    public int ProgressPercent { get; private set; }

    public string? ProgressMessage { get; private set; }

    /// <summary>Partial/Failed 的原因说明（spec §9 失败文档标注原因）。</summary>
    public string? FailureReason { get; private set; }

    /// <summary>解析完成后由后端推断的项目名建议（spec §3.3），未取到为 null。</summary>
    public string? SuggestedName { get; private set; }

    /// <summary>用户是否手动编辑过项目名；true 后前端轮询不得再自动应用 suggestedName。</summary>
    public bool NameEditedByUser { get; private set; }

    /// <summary>两两对比对清单（JSON 数组，元素见 ComparePairItem）。</summary>
    public string? PairsJson { get; private set; }

    /// <summary>解析完成后是否自动进入两两对比；重新解析后置 false，由用户显式「重新对比」。</summary>
    public bool AutoCompareOnParseComplete { get; private set; }

    protected CompareTask()
    {
    }

    public CompareTask(Guid id, string name) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 128);
        Status = CompareTaskStatus.Parsing;
        ProgressStage = "parsing";
        ProgressPercent = 0;
        NameEditedByUser = false;
        AutoCompareOnParseComplete = true;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 128);
        NameEditedByUser = true;
    }

    /// <summary>仅当尚无建议名时填充一次；用户编辑后仍可返回建议名，但前端不再自动应用。</summary>
    public void SetSuggestedName(string? suggestedName)
    {
        if (suggestedName.IsNullOrWhiteSpace())
        {
            return;
        }
        SuggestedName = suggestedName!.Trim();
        if (SuggestedName.Length > 128)
        {
            SuggestedName = SuggestedName[..128];
        }
    }

    public void SetTenderDocument(Guid documentId)
    {
        EnsureStatus(nameof(SetTenderDocument),
            CompareTaskStatus.Parsing, CompareTaskStatus.Parsed,
            CompareTaskStatus.Partial, CompareTaskStatus.AwaitingClauses);
        TenderDocumentId = documentId;
    }

    public void UpdateProgress(string stage, int percent, string? message = null)
    {
        ProgressStage = Check.NotNullOrWhiteSpace(stage, nameof(stage), maxLength: 32);
        ProgressPercent = Math.Clamp(percent, 0, 100);
        ProgressMessage = message;
    }

    public void MarkParsed()
    {
        EnsureStatus(nameof(MarkParsed),
            CompareTaskStatus.Parsing, CompareTaskStatus.Parsed,
            CompareTaskStatus.Partial, CompareTaskStatus.AwaitingClauses);
        Status = CompareTaskStatus.Parsed;
    }

    /// <summary>失败后重新解析：状态回到 Parsing，清空失败原因、进度、旧比对对与报告缓存，且不再自动重跑比对。</summary>
    public void RestartParsing()
    {
        EnsureStatus(nameof(RestartParsing),
            CompareTaskStatus.Failed, CompareTaskStatus.Parsing,
            CompareTaskStatus.Partial, CompareTaskStatus.Parsed,
            CompareTaskStatus.AwaitingClauses);
        Status = CompareTaskStatus.Parsing;
        ProgressStage = "parsing";
        ProgressPercent = 0;
        ProgressMessage = null;
        FailureReason = null;
        PairsJson = null;
        AutoCompareOnParseComplete = false;
        ClearReportCache();
    }

    public void MarkPartial(string reason)
    {
        EnsureStatus(nameof(MarkPartial),
            CompareTaskStatus.Parsing, CompareTaskStatus.Parsed,
            CompareTaskStatus.Partial, CompareTaskStatus.AwaitingClauses,
            CompareTaskStatus.Comparing, CompareTaskStatus.Analyzing);
        Status = CompareTaskStatus.Partial;
        var value = Check.NotNullOrWhiteSpace(reason, nameof(reason));
        FailureReason = value.Length <= 2048 ? value : value[..2048];
    }

    public void MarkFailed(string reason)
    {
        EnsureStatus(nameof(MarkFailed),
            CompareTaskStatus.Parsing, CompareTaskStatus.Parsed, CompareTaskStatus.Partial,
            CompareTaskStatus.Comparing, CompareTaskStatus.Analyzing);
        Status = CompareTaskStatus.Failed;
        var value = Check.NotNullOrWhiteSpace(reason, nameof(reason));
        FailureReason = value.Length <= 2048 ? value : value[..2048];
    }

    public void MarkAwaitingClauses()
    {
        EnsureStatus(nameof(MarkAwaitingClauses), CompareTaskStatus.Parsed, CompareTaskStatus.Partial);
        Status = CompareTaskStatus.AwaitingClauses;
    }

    public void MarkComparing()
    {
        EnsureStatus(nameof(MarkComparing),
            CompareTaskStatus.Parsed, CompareTaskStatus.Partial,
            CompareTaskStatus.AwaitingClauses, CompareTaskStatus.Failed,
            CompareTaskStatus.Done);
        Status = CompareTaskStatus.Comparing;
        // 重新比对将使既有报告过期（证据集变化），清空缓存防止陈旧报告被直接返回
        ClearReportCache();
    }

    public void MarkAnalyzing()
    {
        EnsureStatus(nameof(MarkAnalyzing),
            CompareTaskStatus.Comparing, CompareTaskStatus.Done, CompareTaskStatus.Partial);
        Status = CompareTaskStatus.Analyzing;
    }

    public void MarkDone()
    {
        EnsureStatus(nameof(MarkDone), CompareTaskStatus.Comparing, CompareTaskStatus.Analyzing);
        Status = CompareTaskStatus.Done;
    }

    public void LockClauseSnapshot(string snapshotJson)
    {
        EnsureStatus(nameof(LockClauseSnapshot),
            CompareTaskStatus.Parsing, CompareTaskStatus.Parsed,
            CompareTaskStatus.Partial, CompareTaskStatus.AwaitingClauses);
        ClauseSnapshotJson = Check.NotNullOrWhiteSpace(snapshotJson, nameof(snapshotJson));
    }

    public void SetReport(string reportJson, DateTime generatedAt)
    {
        EnsureStatus(nameof(SetReport), CompareTaskStatus.Done);
        ReportJson = Check.NotNullOrWhiteSpace(reportJson, nameof(reportJson));
        ReportGeneratedAt = generatedAt;
    }

    /// <summary>首次/全量重跑前按标书组合初始化全部比对对（waiting）。</summary>
    public void InitializePairs(IReadOnlyList<(Guid DocA, Guid DocB)> pairs)
    {
        var items = pairs.Select(p => new ComparePairItem
        {
            PairId = Guid.NewGuid(),
            DocAId = p.DocA,
            DocBId = p.DocB,
            Status = ComparePairStatus.Waiting
        }).ToList();
        PairsJson = JsonSerializer.Serialize(items, PairJsonOptions);
    }

    /// <summary>部分重跑前仅复位指定比对对为 waiting，其余对状态与结果保留。</summary>
    public void ResetPairsForRetry(IReadOnlyCollection<Guid> pairIds)
    {
        var items = GetPairs();
        foreach (var item in items.Where(i => pairIds.Contains(i.PairId)))
        {
            item.Status = ComparePairStatus.Waiting;
            item.Similarity = null;
            item.FailReason = null;
            item.StartedAt = null;
            item.FinishedAt = null;
        }
        PairsJson = JsonSerializer.Serialize(items, PairJsonOptions);
    }

    public List<ComparePairItem> GetPairs()
        => PairsJson == null
            ? new List<ComparePairItem>()
            : JsonSerializer.Deserialize<List<ComparePairItem>>(PairsJson, PairJsonOptions) ?? new();

    public void MarkPairProcessing(Guid pairId, DateTime startedAt)
    {
        var items = GetPairs();
        var item = items.FirstOrDefault(p => p.PairId == pairId);
        if (item == null)
        {
            return;
        }
        item.Status = ComparePairStatus.Processing;
        item.StartedAt = startedAt;
        item.FinishedAt = null;
        PairsJson = JsonSerializer.Serialize(items, PairJsonOptions);
    }

    /// <summary>批处理模式下记录对的开始时间（算法一次分析全部对，不逐对置 processing）。</summary>
    public void MarkPairBatchStarted(IEnumerable<Guid> pairIds, DateTime startedAt)
    {
        var items = GetPairs();
        foreach (var item in items.Where(i => pairIds.Contains(i.PairId) && i.StartedAt == null))
        {
            item.StartedAt = startedAt;
        }
        PairsJson = JsonSerializer.Serialize(items, PairJsonOptions);
    }

    public void MarkPairDone(Guid pairId, DateTime finishedAt, double? similarity)
    {
        var items = GetPairs();
        var item = items.FirstOrDefault(p => p.PairId == pairId);
        if (item == null)
        {
            return;
        }
        item.Status = ComparePairStatus.Done;
        item.Similarity = similarity;
        item.FailReason = null;
        item.FinishedAt = finishedAt;
        PairsJson = JsonSerializer.Serialize(items, PairJsonOptions);
    }

    public void MarkPairFailed(Guid pairId, DateTime finishedAt, string reason)
    {
        var items = GetPairs();
        var item = items.FirstOrDefault(p => p.PairId == pairId);
        if (item == null)
        {
            return;
        }
        item.Status = ComparePairStatus.Failed;
        item.Similarity = null;
        item.FailReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), maxLength: 2048);
        item.FinishedAt = finishedAt;
        PairsJson = JsonSerializer.Serialize(items, PairJsonOptions);
    }

    private void ClearReportCache()
    {
        ReportJson = null;
        ReportGeneratedAt = null;
    }

    private void EnsureStatus(string action, params CompareTaskStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new BusinessException(BidCompareErrorCodes.InvalidTaskState)
                .WithData("action", action)
                .WithData("status", Status.ToString());
        }
    }
}
