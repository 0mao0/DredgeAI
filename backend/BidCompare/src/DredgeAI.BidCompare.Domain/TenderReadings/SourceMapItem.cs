using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>字段原文锚点（blockId / pageIdx / bbox 0~1 归一化矩形）。</summary>
public class SourceMapItem : FullAuditedEntity<Guid>
{
    public Guid FieldId { get; private set; }

    /// <summary>AnGIneer block_uid。</summary>
    public string BlockId { get; private set; } = default!;

    /// <summary>0 基页码，与 IR 一致。</summary>
    public int PageIdx { get; private set; }

    /// <summary>0~1 归一化矩形 [x0,y0,x1,y1]，JSON 数组字符串。</summary>
    public string BboxJson { get; private set; } = default!;

    /// <summary>原文片段。</summary>
    public string Text { get; private set; } = default!;

    protected SourceMapItem()
    {
    }

    public SourceMapItem(
        Guid id,
        Guid fieldId,
        string blockId,
        int pageIdx,
        string bboxJson,
        string text) : base(id)
    {
        FieldId = fieldId;
        BlockId = Check.NotNullOrWhiteSpace(blockId, nameof(blockId), maxLength: 128);
        PageIdx = pageIdx;
        BboxJson = Check.NotNullOrWhiteSpace(bboxJson, nameof(bboxJson));
        Text = text ?? string.Empty;
        if (Text.Length > 4000)
        {
            Text = Text[..4000];
        }
    }
}
