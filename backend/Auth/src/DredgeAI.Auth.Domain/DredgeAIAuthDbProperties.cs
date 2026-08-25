namespace DredgeAI;

public static class DredgeAIAuthDbProperties
{
    public static string DbTablePrefix { get; set; } = "DredgeAIAuth";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "DredgeAIAuth";
}