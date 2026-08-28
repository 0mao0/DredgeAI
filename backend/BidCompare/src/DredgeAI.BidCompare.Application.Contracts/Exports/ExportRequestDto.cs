using System.ComponentModel.DataAnnotations;

namespace DredgeAI.BidCompare.Exports;

/// <summary>spec §6：{ format: 'pdf'|'word' }（枚举整型：0=Pdf, 1=Word）。</summary>
public class ExportRequestDto
{
    [Required]
    public ExportFormat Format { get; set; }
}
