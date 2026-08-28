using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.MeetingBot;

/// <summary>点名时未识别人脸的裁剪图（按 bbox 从前端照片裁出），供报告展示与后续人脸入库。</summary>
public class UnrecognizedFace : FullAuditedEntity<Guid>
{
    public Guid MeetingRecordId { get; private set; }

    /// <summary>裁剪图存储 key（IFileStorage）。</summary>
    public string PhotoKey { get; private set; } = "";

    public double Confidence { get; private set; }

    /// <summary>人脸框 [x1, y1, x2, y2] 的 JSON 序列化。</summary>
    public string BboxJson { get; private set; } = "[]";

    protected UnrecognizedFace()
    {
    }

    public UnrecognizedFace(Guid id, Guid meetingRecordId, string photoKey, double confidence, double[]? bbox = null)
        : base(id)
    {
        MeetingRecordId = meetingRecordId;
        PhotoKey = photoKey;
        Confidence = confidence;
        BboxJson = bbox is { Length: 4 } ? System.Text.Json.JsonSerializer.Serialize(bbox) : "[]";
    }
}
