using System;
using System.Linq;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>读标任务聚合根（P1：创建、上传、解析、抽取、状态查询）。</summary>
public class TenderReadingTask : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; private set; } = default!;

    /// <summary>项目编号（如 ZB-2026-008），抽取后回填。</summary>
    public string? ProjectCode { get; private set; }

    public TenderReadingTaskStatus Status { get; private set; }

    public string ProgressStage { get; private set; } = "uploading";

    public int ProgressPercent { get; private set; }

    public string? FailureReason { get; private set; }

    protected TenderReadingTask()
    {
    }

    public TenderReadingTask(Guid id, string name) : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 128);
        Status = TenderReadingTaskStatus.Uploading;
        ProgressStage = "uploading";
        ProgressPercent = 5;
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: 128);
    }

    public void SetProjectCode(string? projectCode)
    {
        ProjectCode = string.IsNullOrWhiteSpace(projectCode) ? null : projectCode.Trim();
        if (ProjectCode?.Length > 64)
        {
            ProjectCode = ProjectCode[..64];
        }
    }

    public void StartParsing()
    {
        EnsureStatus(nameof(StartParsing),
            TenderReadingTaskStatus.Uploading,
            TenderReadingTaskStatus.Parsed,
            TenderReadingTaskStatus.Partial,
            TenderReadingTaskStatus.Failed,
            TenderReadingTaskStatus.Ready,
            TenderReadingTaskStatus.Reviewing);
        Status = TenderReadingTaskStatus.Parsing;
        ProgressStage = "parsing";
        ProgressPercent = 10;
        FailureReason = null;
    }

    public void UpdateProgress(string stage, int percent, string? message = null)
    {
        ProgressStage = Check.NotNullOrWhiteSpace(stage, nameof(stage), maxLength: 32);
        ProgressPercent = Math.Clamp(percent, 0, 100);
        if (!string.IsNullOrWhiteSpace(message))
        {
            FailureReason = message!.Length <= 2048 ? message : message[..2048];
        }
    }

    public void MarkParsed()
    {
        EnsureStatus(nameof(MarkParsed), TenderReadingTaskStatus.Parsing, TenderReadingTaskStatus.Parsed);
        Status = TenderReadingTaskStatus.Parsed;
        ProgressStage = "parsed";
        ProgressPercent = 40;
        FailureReason = null;
    }

    public void StartExtracting()
    {
        EnsureStatus(nameof(StartExtracting), TenderReadingTaskStatus.Parsed, TenderReadingTaskStatus.Extracting, TenderReadingTaskStatus.Partial, TenderReadingTaskStatus.Ready);
        Status = TenderReadingTaskStatus.Extracting;
        ProgressStage = "extracting";
        ProgressPercent = 45;
        FailureReason = null;
    }

    public void MarkReviewing()
    {
        EnsureStatus(nameof(MarkReviewing), TenderReadingTaskStatus.Extracting, TenderReadingTaskStatus.Partial);
        Status = TenderReadingTaskStatus.Reviewing;
        ProgressStage = "reviewing";
        ProgressPercent = 85;
    }

    public void MarkReady()
    {
        EnsureStatus(nameof(MarkReady), TenderReadingTaskStatus.Extracting, TenderReadingTaskStatus.Partial);
        Status = TenderReadingTaskStatus.Ready;
        ProgressStage = "ready";
        ProgressPercent = 100;
        FailureReason = null;
    }

    public void MarkPartial(string reason)
    {
        EnsureStatus(nameof(MarkPartial),
            TenderReadingTaskStatus.Extracting,
            TenderReadingTaskStatus.Parsing,
            TenderReadingTaskStatus.Parsed,
            TenderReadingTaskStatus.Partial);
        Status = TenderReadingTaskStatus.Partial;
        var value = Check.NotNullOrWhiteSpace(reason, nameof(reason));
        FailureReason = value.Length <= 2048 ? value : value[..2048];
        ProgressStage = "partial";
        ProgressPercent = 100;
    }

    public void MarkFailed(string reason)
    {
        EnsureStatus(nameof(MarkFailed),
            TenderReadingTaskStatus.Uploading,
            TenderReadingTaskStatus.Parsing,
            TenderReadingTaskStatus.Parsed,
            TenderReadingTaskStatus.Extracting,
            TenderReadingTaskStatus.Partial);
        Status = TenderReadingTaskStatus.Failed;
        var value = Check.NotNullOrWhiteSpace(reason, nameof(reason));
        FailureReason = value.Length <= 2048 ? value : value[..2048];
        ProgressStage = "failed";
        ProgressPercent = 100;
    }

    private void EnsureStatus(string action, params TenderReadingTaskStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new BusinessException(TenderReadErrorCodes.InvalidTaskState)
                .WithData("action", action)
                .WithData("status", Status.ToString());
        }
    }
}
