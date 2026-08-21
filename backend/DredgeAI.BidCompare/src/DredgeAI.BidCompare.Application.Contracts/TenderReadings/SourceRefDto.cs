using System;

namespace DredgeAI.BidCompare.TenderReadings;

public class SourceRefDto
{
    public Guid FieldId { get; set; }

    /// <summary>AnGIneer block_uid。</summary>
    public string BlockId { get; set; } = default!;

    /// <summary>0 基页码。</summary>
    public int PageIdx { get; set; }

    /// <summary>0~1 归一化矩形 [x0,y0,x1,y1]。</summary>
    public double[] Bbox { get; set; } = System.Array.Empty<double>();

    public string Text { get; set; } = default!;
}
