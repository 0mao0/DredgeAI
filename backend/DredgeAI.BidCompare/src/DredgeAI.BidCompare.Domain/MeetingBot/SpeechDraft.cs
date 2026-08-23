using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>晨会稿草稿。</summary>
public class SpeechDraft : FullAuditedEntity<Guid>
{
    public Guid MeetingRecordId { get; private set; }

    public string Content { get; private set; } = "";

    public string Status { get; private set; } = "draft";

    public DateTime UpdatedAt { get; private set; }

    protected SpeechDraft()
    {
    }

    public SpeechDraft(Guid id, Guid meetingRecordId, string content) : base(id)
    {
        MeetingRecordId = meetingRecordId;
        SetContent(content);
    }

    public void SetContent(string content)
    {
        Content = content ?? "";
        Status = "generated";
        UpdatedAt = DateTime.Now;
    }

    public void Confirm()
    {
        Status = "confirmed";
        UpdatedAt = DateTime.Now;
    }
}
