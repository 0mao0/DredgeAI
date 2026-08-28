using System.ComponentModel.DataAnnotations;

namespace DredgeAI.BidCompare.ClauseTemplates;

public class ClauseTemplateCreateUpdateDto
{
    [Required]
    [StringLength(2000)]
    public string Text { get; set; } = default!;

    public bool Mandatory { get; set; } = true;

    [StringLength(64)]
    public string? Category { get; set; }
}
