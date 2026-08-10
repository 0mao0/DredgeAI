using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.CompareTasks;

public class CompareTask : FullAuditedAggregateRoot<Guid>
{
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

    protected CompareTask()
    {
    }

    public CompareTask(Guid id, string name) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 128);
        Status = CompareTaskStatus.Parsing;
        ProgressStage = "parsing";
        ProgressPercent = 0;
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

    public void MarkPartial(string reason)
    {
        EnsureStatus(nameof(MarkPartial),
            CompareTaskStatus.Parsing, CompareTaskStatus.Parsed,
            CompareTaskStatus.Partial, CompareTaskStatus.AwaitingClauses);
        Status = CompareTaskStatus.Partial;
        FailureReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), maxLength: 2048);
    }

    public void MarkFailed(string reason)
    {
        EnsureStatus(nameof(MarkFailed),
            CompareTaskStatus.Parsing, CompareTaskStatus.Comparing, CompareTaskStatus.Analyzing);
        Status = CompareTaskStatus.Failed;
        FailureReason = Check.NotNullOrWhiteSpace(reason, nameof(reason), maxLength: 2048);
    }

    public void MarkAwaitingClauses()
    {
        EnsureStatus(nameof(MarkAwaitingClauses), CompareTaskStatus.Parsed, CompareTaskStatus.Partial);
        Status = CompareTaskStatus.AwaitingClauses;
    }

    public void MarkComparing()
    {
        EnsureStatus(nameof(MarkComparing),
            CompareTaskStatus.Parsed, CompareTaskStatus.Partial, CompareTaskStatus.AwaitingClauses);
        Status = CompareTaskStatus.Comparing;
    }

    public void MarkAnalyzing()
    {
        EnsureStatus(nameof(MarkAnalyzing), CompareTaskStatus.Comparing);
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
