using Volo.Abp.Reflection;

namespace DredgeAI.Gateway.Permissions;

public static class GatewayPermissions
{
    public const string GroupName = "Gateway";

    public static class ProxyRoutes
    {
        public const string Default = GroupName + ".ProxyRoutes";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    public static class ProxyClusters
    {
        public const string Default = GroupName + ".ProxyClusters";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    public static class RateLimitPolicies
    {
        public const string Default = GroupName + ".RateLimitPolicies";
        public const string Create = Default + ".Create";
        public const string Update = Default + ".Update";
        public const string Delete = Default + ".Delete";
    }

    public static string[] GetAll()
    {
        return ReflectionHelper.GetPublicConstantsRecursively(typeof(GatewayPermissions));
    }
}
