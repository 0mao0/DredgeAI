namespace DredgeAI.BidCompare;

public static class BidCompareDbProperties
{
    public static string DbTablePrefix { get; set; } = "tab_";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "Default";
}
