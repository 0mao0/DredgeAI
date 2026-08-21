using System.ComponentModel.DataAnnotations;

namespace DredgeAI.BidCompare.TenderReadings;

public class UpdateTenderReadingTaskInput
{
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = default!;

    [StringLength(64)]
    public string? ProjectCode { get; set; }
}
