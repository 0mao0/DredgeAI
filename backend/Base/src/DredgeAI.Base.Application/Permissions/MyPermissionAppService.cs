using Microsoft.Extensions.Options;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SimpleStateChecking;

namespace DredgeAI.Permissions;

/// <summary>
/// 权限管理应用服务，替换 ABP 内置 <see cref="PermissionAppService"/>，
/// 新增批量 <c>UpdateResourceAsync</c>：同一资源多个 Key 应用同一组授权。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IPermissionAppService), typeof(IMyPermissionAppService))]
public class MyPermissionAppService : PermissionAppService, IMyPermissionAppService
{
    public MyPermissionAppService(
        IPermissionManager permissionManager,
        IPermissionChecker permissionChecker,
        IPermissionDefinitionManager permissionDefinitionManager,
        IResourcePermissionManager resourcePermissionManager,
        IResourcePermissionGrantRepository resourcePermissionGrantRepository,
        IOptions<PermissionManagementOptions> options,
        ISimpleStateCheckerManager<PermissionDefinition> simpleStateCheckerManager)
        : base(
            permissionManager,
            permissionChecker,
            permissionDefinitionManager,
            resourcePermissionManager,
            resourcePermissionGrantRepository,
            options,
            simpleStateCheckerManager)
    {
    }

    public override async Task UpdateResourceAsync(string resourceName, string resourceKey, UpdateResourcePermissionsDto input)
    {
        var permissions = await GetManageableResourcePermissionsAsync(resourceName);
        await SetResourcePermissionsAsync(resourceName, resourceKey, permissions, input);
    }

    /// <summary>批量更新同一资源下多个 Key 的权限授予状态（同一工作单元内，全部成功或全部回滚）。</summary>
    public virtual async Task UpdateResourceAsync(string resourceName, List<string> resourceKeys, UpdateResourcePermissionsDto input)
    {
        if (resourceKeys == null || resourceKeys.Count == 0)
        {
            return;
        }

        var permissions = await GetManageableResourcePermissionsAsync(resourceName);
        foreach (var resourceKey in resourceKeys)
        {
            await SetResourcePermissionsAsync(resourceName, resourceKey, permissions, input);
        }
    }

    /// <summary>
    /// 获取当前用户拥有管理权限（ManagementPermissionName）的资源权限定义；
    /// 无管理权限的定义跳过，与 ABP 内置单 Key 更新逻辑一致。
    /// </summary>
    protected virtual async Task<List<PermissionDefinition>> GetManageableResourcePermissionsAsync(string resourceName)
    {
        var resourcePermissions = await ResourcePermissionManager.GetAvailablePermissionsAsync(resourceName);
        var grantedManagementPermissions = (await PermissionChecker.IsGrantedAsync(
                resourcePermissions.Select(rp => rp.ManagementPermissionName!).Distinct().ToArray()))
            .Result
            .Where(x => x.Value == PermissionGrantResult.Granted)
            .Select(x => x.Key)
            .ToHashSet();

        return resourcePermissions
            .Where(rp => grantedManagementPermissions.Contains(rp.ManagementPermissionName!))
            .ToList();
    }

    /// <summary>对单个 Key 全量覆盖：input.Permissions 中的授予，列表外的撤销。</summary>
    protected virtual async Task SetResourcePermissionsAsync(
        string resourceName,
        string resourceKey,
        List<PermissionDefinition> resourcePermissions,
        UpdateResourcePermissionsDto input)
    {
        foreach (var resourcePermission in resourcePermissions)
        {
            var isGranted = !input.Permissions.IsNullOrEmpty() && input.Permissions.Any(p => p == resourcePermission.Name);
            await ResourcePermissionManager.SetAsync(
                resourcePermission.Name,
                resourceName,
                resourceKey,
                input.ProviderName,
                input.ProviderKey,
                isGranted);
        }
    }
}
