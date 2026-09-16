using DredgeAI.Gateway.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;

namespace DredgeAI.Gateway.Permissions;

public class GatewayPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var group = context.AddGroup(GatewayPermissions.GroupName, L("Permission:Gateway"));

        var routes = group.AddPermission(GatewayPermissions.ProxyRoutes.Default, L("Permission:Gateway.ProxyRoutes"));
        routes.AddChild(GatewayPermissions.ProxyRoutes.Create, L("Permission:Gateway.ProxyRoutes.Create"));
        routes.AddChild(GatewayPermissions.ProxyRoutes.Update, L("Permission:Gateway.ProxyRoutes.Update"));
        routes.AddChild(GatewayPermissions.ProxyRoutes.Delete, L("Permission:Gateway.ProxyRoutes.Delete"));

        var clusters = group.AddPermission(GatewayPermissions.ProxyClusters.Default, L("Permission:Gateway.ProxyClusters"));
        clusters.AddChild(GatewayPermissions.ProxyClusters.Create, L("Permission:Gateway.ProxyClusters.Create"));
        clusters.AddChild(GatewayPermissions.ProxyClusters.Update, L("Permission:Gateway.ProxyClusters.Update"));
        clusters.AddChild(GatewayPermissions.ProxyClusters.Delete, L("Permission:Gateway.ProxyClusters.Delete"));

        var policies = group.AddPermission(GatewayPermissions.RateLimitPolicies.Default, L("Permission:Gateway.RateLimitPolicies"));
        policies.AddChild(GatewayPermissions.RateLimitPolicies.Create, L("Permission:Gateway.RateLimitPolicies.Create"));
        policies.AddChild(GatewayPermissions.RateLimitPolicies.Update, L("Permission:Gateway.RateLimitPolicies.Update"));
        policies.AddChild(GatewayPermissions.RateLimitPolicies.Delete, L("Permission:Gateway.RateLimitPolicies.Delete"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<GatewayResource>(name);
    }
}
