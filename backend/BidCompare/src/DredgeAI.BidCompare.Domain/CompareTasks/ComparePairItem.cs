using System;

namespace DredgeAI.BidCompare.CompareTasks;

/// <summary>
/// 任务内两两对比对（序列化进 CompareTask.PairsJson）。
/// JSON 字段名 camelCase：pairId/docAId/docBId/status/similarity/failReason/startedAt/finishedAt。
/// </summary>
public class ComparePairItem
{
    public Guid PairId { get; set; }

    public Guid DocAId { get; set; }

    public Guid DocBId { get; set; }

    public ComparePairStatus Status { get; set; }

    public double? Similarity { get; set; }

    public string? FailReason { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? FinishedAt { get; set; }
}
