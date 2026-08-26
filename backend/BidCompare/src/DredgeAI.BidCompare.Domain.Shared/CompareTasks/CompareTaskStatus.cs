namespace DredgeAI.BidCompare.CompareTasks;

// spec §5: parsing → parsed → (待条款确认) → comparing → analyzing → done；异常态 failed/partial
public enum CompareTaskStatus : byte
{
    Parsing = 0,
    Parsed = 1,
    AwaitingClauses = 2,
    Comparing = 3,
    Analyzing = 4,
    Done = 5,
    Failed = 6,
    Partial = 7
}
