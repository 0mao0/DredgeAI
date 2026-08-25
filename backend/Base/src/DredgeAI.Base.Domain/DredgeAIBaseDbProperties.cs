namespace DredgeAI;

public static class DredgeAIBaseDbProperties
{
    public static string DbTablePrefix { get; set; } = "DredgeAIBase";

    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "DredgeAIBase";
}