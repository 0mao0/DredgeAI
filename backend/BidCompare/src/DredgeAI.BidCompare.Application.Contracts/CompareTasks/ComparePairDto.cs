using System;

namespace DredgeAI.BidCompare.CompareTasks;

/// <summary>两两对比对 DTO（spec §8.2 逐对进度契约）。</summary>
public class ComparePairDto
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
