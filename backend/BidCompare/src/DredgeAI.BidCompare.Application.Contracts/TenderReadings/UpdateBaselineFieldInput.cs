using System.ComponentModel.DataAnnotations;

namespace DredgeAI.BidCompare.TenderReadings;

public class UpdateBaselineFieldInput
{
    [Required]
    public string ValueJson { get; set; } = default!;

    public string? RawText { get; set; }

    /// <summary>人工操作后的字段状态：confirmed（确认）或 edited（修改）。</summary>
    [Required]
    public BaselineFieldStatus Status { get; set; }

    public double? Confidence { get; set; }
}
