namespace DredgeAI.BidCompare.Clauses;

/// <summary>
/// 任务内条款快照元素（spec §6.1 Clause，序列化进 CompareTask.ClauseSnapshotJson）。
/// JSON 字段名 camelCase：clauseId/source/text/mandatory/category。
/// </summary>
public class ClauseSnapshotItem
{
    public string ClauseId { get; set; } = default!;

    public ClauseSource Source { get; set; }

    public string Text { get; set; } = default!;

    public bool Mandatory { get; set; }

    public string? Category { get; set; }
}
