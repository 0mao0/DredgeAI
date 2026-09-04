namespace DredgeAI.Gateway;

public static class GatewayDbProperties
{
    public static string DbTablePrefix { get; set; } = "tab_";

    // schema 由连接串 SearchPath=dredge_gateway 决定
    public static string? DbSchema { get; set; } = null;

    public const string ConnectionStringName = "Default";
}
