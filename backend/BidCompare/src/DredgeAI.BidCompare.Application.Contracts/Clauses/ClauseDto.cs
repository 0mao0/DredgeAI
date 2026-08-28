namespace DredgeAI.BidCompare.Clauses;

public class ClauseDto
{
    public string ClauseId { get; set; } = default!;

    public ClauseSource Source { get; set; }

    public string Text { get; set; } = default!;

    public bool Mandatory { get; set; }

    public string? Category { get; set; }
}
