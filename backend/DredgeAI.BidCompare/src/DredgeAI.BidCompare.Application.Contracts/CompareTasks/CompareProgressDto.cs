namespace DredgeAI.BidCompare.CompareTasks;

public class CompareProgressDto
{
    public string Stage { get; set; } = "parsing";

    public int Percent { get; set; }

    public string? Message { get; set; }
}
