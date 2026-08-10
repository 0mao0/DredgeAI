using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DredgeAI.BidCompare.Clauses;

namespace DredgeAI.BidCompare.CompareTasks;

public class CreateCompareTaskDto
{
    [Required]
    [StringLength(128)]
    public string Name { get; set; } = default!;

    /// <summary>spec §6「创建任务（含条款清单快照）」：可选，提供即锁定快照。</summary>
    public List<ClauseInputDto>? Clauses { get; set; }
}
