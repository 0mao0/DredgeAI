using System;
using System.Text.Json;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>点名记录（同一会议+工人去重；未识别人脸按 bbox 位置去重，供后续人脸入库）。</summary>
public class AttendanceRecord : FullAuditedEntity<Guid>
{
    public Guid MeetingRecordId { get; private set; }

    public Guid? WorkerId { get; private set; }

    public string Name { get; private set; } = "";

    public string Team { get; private set; } = "";

    public AttendanceStatus Status { get; private set; }

    public double Confidence { get; private set; }

    /// <summary>人脸框 [x1, y1, x2, y2] 的 JSON 序列化，未识别人脸去重用；无坐标时为 "[]"。</summary>
    public string Bbox { get; private set; } = "[]";

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
        double confidence,
        double[]? bbox = null) : base(id)
    {
        MeetingRecordId = meetingRecordId;
        WorkerId = workerId;
        Name = name ?? "";
        Team = team ?? "";
        Status = status;
        Confidence = confidence;
        Bbox = bbox is { Length: 4 } ? JsonSerializer.Serialize(bbox) : "[]";
    }
}
