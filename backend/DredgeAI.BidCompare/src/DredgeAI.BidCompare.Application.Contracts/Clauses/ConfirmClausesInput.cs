using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DredgeAI.BidCompare.Clauses;

/// <summary>PUT clauses 请求体：用户确认后的条款清单（全量，含勾选/编辑/从条款库追加的结果）。</summary>
public class ConfirmClausesInput
{
    [Required]
    [MinLength(1)]
    public List<ClauseInputDto> Clauses { get; set; } = new();
}
