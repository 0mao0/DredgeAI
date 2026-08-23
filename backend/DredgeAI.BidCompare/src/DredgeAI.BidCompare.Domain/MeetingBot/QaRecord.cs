using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>会议问答记录。</summary>
public class QaRecord : FullAuditedEntity<Guid>
{
    public Guid MeetingRecordId { get; private set; }

    public string Question { get; private set; } = "";

    public string Answer { get; private set; } = "";

    public QaIntentType IntentType { get; private set; }

    /// <summary>证据来源（文件名/页码 JSON 数组）。</summary>
    public string SourcesJson { get; private set; } = "[]";

    public DateTime CreatedAt { get; private set; }

    protected QaRecord()
    {
    }

    public QaRecord(
        Guid id,
        Guid meetingRecordId,
        string question,
        string answer,
        QaIntentType intentType,
        string sourcesJson) : base(id)
    {
        MeetingRecordId = meetingRecordId;
        Question = question ?? "";
        Answer = answer ?? "";
        IntentType = intentType;
        SourcesJson = string.IsNullOrWhiteSpace(sourcesJson) ? "[]" : sourcesJson;
        CreatedAt = DateTime.Now;
    }
}
