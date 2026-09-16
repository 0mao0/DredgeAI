namespace DredgeAI.Gateway;

public static class GatewayErrorCodes
{
    public const string DuplicateRouteId = "Gateway:DuplicateRouteId";
    public const string DuplicateClusterId = "Gateway:DuplicateClusterId";
    public const string ClusterNotFound = "Gateway:ClusterNotFound";
    public const string ClusterInUse = "Gateway:ClusterInUse";
    public const string InvalidDestinationAddress = "Gateway:InvalidDestinationAddress";
    public const string DuplicateRateLimitPolicyName = "Gateway:DuplicateRateLimitPolicyName";
    public const string GlobalRateLimitPolicyExists = "Gateway:GlobalRateLimitPolicyExists";
    public const string RouteRateLimitPolicyExists = "Gateway:RouteRateLimitPolicyExists";
    public const string RouteNotFound = "Gateway:RouteNotFound";
    public const string InvalidRateLimitParameters = "Gateway:InvalidRateLimitParameters";
}
