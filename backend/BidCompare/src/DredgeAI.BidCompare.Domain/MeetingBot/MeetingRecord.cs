using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>AI 晨会记录聚合根。</summary>
public class MeetingRecord : FullAuditedAggregateRoot<Guid>
{
    public DateTime Date { get; private set; }

    public string PreInfoJson { get; private set; } = "{}";

    public MeetingStatus Status { get; private set; }

    public DateTime? StartedAt { get; private set; }

    public DateTime? EndedAt { get; private set; }

    public Guid? SpeechDraftId { get; private set; }

    /// <summary>会议全程录音存储 key（IFileStorage）。</summary>
    public string? TranscriptFile { get; private set; }

    /// <summary>转写文本（后台任务回填）。</summary>
    public string? TranscriptText { get; private set; }

    /// <summary>Markdown 报告存储 key。</summary>
    public string? ReportFile { get; private set; }

    /// <summary>报告生成失败原因。</summary>
    public string? ReportError { get; private set; }

    protected MeetingRecord()
    {
    }

    public MeetingRecord(Guid id, DateTime date, string preInfoJson) : base(id)
    {
        Date = date;
        PreInfoJson = string.IsNullOrWhiteSpace(preInfoJson) ? "{}" : preInfoJson;
        Status = MeetingStatus.Draft;
    }

    public void AttachSpeechDraft(Guid speechDraftId)
    {
        SpeechDraftId = speechDraftId;
    }

    public void MarkPrepared()
    {
        Status = MeetingStatus.Prepared;
    }

    public void StartRollcall()
    {
        Status = MeetingStatus.Rollcall;
        StartedAt ??= DateTime.Now;
    }

    public void MarkOngoing()
    {
        Status = MeetingStatus.Ongoing;
    }

    public void SetRecording(string transcriptFile)
    {
        TranscriptFile = transcriptFile;
    }

    public void Complete()
    {
        Status = MeetingStatus.Completed;
        EndedAt = DateTime.Now;
    }

    public void SetTranscript(string? transcriptText)
    {
        TranscriptText = transcriptText;
    }

    public void SetReport(string? reportFile, string? error = null)
    {
        ReportFile = reportFile;
        ReportError = error;
    }
}
