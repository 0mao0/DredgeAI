using System.ComponentModel.DataAnnotations;

namespace DredgeAI.BidCompare.TenderReadings;

public class ReExtractBaselineInput
{
    /// <summary>指定重新抽取的字段类别；为空时全量重抽。</summary>
    public BaselineCategory? Category { get; set; }
}
