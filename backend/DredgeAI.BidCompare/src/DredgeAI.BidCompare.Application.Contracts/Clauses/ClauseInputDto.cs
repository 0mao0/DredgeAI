using System.ComponentModel.DataAnnotations;

namespace DredgeAI.BidCompare.Clauses;

public class ClauseInputDto
{
    /// <summary>可空：新增条款由服务端生成；从草案/模板带过来的条款保留原 id。</summary>
    public string? ClauseId { get; set; }

    /// <summary>可空：默认 Manual（extracted/template 由前端透传）。</summary>
    public ClauseSource? Source { get; set; }

    [Required]
    [StringLength(2000)]
    public string Text { get; set; } = default!;

    public bool Mandatory { get; set; } = true;

    [StringLength(64)]
    public string? Category { get; set; }
}
