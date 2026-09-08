namespace DredgeAI.Gateway;

public static class GatewayErrorCodes
{
    public const string DuplicateRouteId = "Gateway:DuplicateRouteId";
    public const string DuplicateClusterId = "Gateway:DuplicateClusterId";
    public const string ClusterNotFound = "Gateway:ClusterNotFound";
    public const string ClusterInUse = "Gateway:ClusterInUse";
    public const string InvalidDestinationAddress = "Gateway:InvalidDestinationAddress";
}
