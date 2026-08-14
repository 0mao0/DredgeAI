namespace DredgeAI.BidCompare.CompareTasks;

public class CompareProgressDto
{
    public string Stage { get; set; } = "parsing";

    public int Percent { get; set; }

    public string? Message { get; set; }

    /// <summary>当前比对对序号（1 起）；尚无逐对数据时为 null。</summary>
    public int? PairIndex { get; set; }

    /// <summary>比对对总数；尚无逐对数据时为 null。</summary>
    public int? PairCount { get; set; }
}
