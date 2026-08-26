namespace DredgeAI.BidCompare.CompareTasks;

/// <summary>两两对比对状态（spec §8.2 逐对进度契约）。</summary>
public enum ComparePairStatus : byte
{
    Waiting = 0,
    Processing = 1,
    Done = 2,
    Failed = 3
}
