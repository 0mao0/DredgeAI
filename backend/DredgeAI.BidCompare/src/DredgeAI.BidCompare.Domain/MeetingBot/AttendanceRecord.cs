using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>点名记录（同一会议+工人去重）。</summary>
public class AttendanceRecord : FullAuditedEntity<Guid>
{
    public Guid MeetingRecordId { get; private set; }

    public Guid? WorkerId { get; private set; }

    public string Name { get; private set; } = "";

    public string Team { get; private set; } = "";

    public AttendanceStatus Status { get; private set; }

    public double Confidence { get; private set; }

    protected AttendanceRecord()
    {
    }

    public AttendanceRecord(
        Guid id,
        Guid meetingRecordId,
        Guid? workerId,
        string name,
        string team,
        AttendanceStatus status,
        double confidence) : base(id)
    {
        MeetingRecordId = meetingRecordId;
        WorkerId = workerId;
        Name = name ?? "";
        Team = team ?? "";
        Status = status;
        Confidence = confidence;
    }
}
