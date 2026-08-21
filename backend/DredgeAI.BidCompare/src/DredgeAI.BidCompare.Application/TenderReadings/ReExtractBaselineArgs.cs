using System;

namespace DredgeAI.BidCompare.TenderReadings;

/// <summary>重抽基准库后台任务参数（Category 为空时全量重抽）。</summary>
public class ReExtractBaselineArgs
{
    public Guid TaskId { get; set; }

    public Guid DocumentId { get; set; }

    public BaselineCategory? Category { get; set; }
}
