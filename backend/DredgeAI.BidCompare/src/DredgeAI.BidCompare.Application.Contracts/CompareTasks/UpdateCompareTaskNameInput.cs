using System.ComponentModel.DataAnnotations;

namespace DredgeAI.BidCompare.CompareTasks;

/// <summary>编辑任务项目名（spec §3.3 智能修正归属：用户编辑后置 nameEditedByUser）。</summary>
public class UpdateCompareTaskNameInput
{
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = default!;
}
