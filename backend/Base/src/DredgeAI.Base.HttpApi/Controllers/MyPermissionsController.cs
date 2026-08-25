using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.DependencyInjection;
using Volo.Abp.PermissionManagement;

namespace DredgeAI.Controllers;

/// <summary>
/// 权限管理控制器，替换 ABP 内置的 <see cref="PermissionsController"/>，
/// 通过 PermissionManagementOptions.ProviderPolicies 配置映射到 Base 模块自定义权限。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(PermissionsController), IncludeSelf = true)]
[Route($"api/{DredgeAIBaseRemoteServiceConsts.ModuleName}/permission-management/permissions")]
[Tags("权限管理")]
public class MyPermissionsController : PermissionsController
{
    public MyPermissionsController(IPermissionAppService permissionAppService) : base(permissionAppService)
    {
    }

    /// <summary>
    /// 获取指定 Provider 下的完整权限树，包含每个权限的授予状态。
    /// </summary>
    /// <param name="providerName">权限提供者名称（如 "R" 表示角色、"U" 表示用户）。</param>
    /// <param name="providerKey">权限提供者 Key（如角色名或用户ID）。</param>
    /// <returns>权限树结构及每个权限的授予状态。</returns>
    [HttpGet]
    public override Task<GetPermissionListResultDto> GetAsync(string providerName, string providerKey)
    {
        return PermissionAppService.GetAsync(providerName, providerKey);
    }

    /// <summary>
    /// 获取指定权限组下的权限列表及授予状态。
    /// </summary>
    /// <param name="groupName">权限组名称。</param>
    /// <param name="providerName">权限提供者名称。</param>
    /// <param name="providerKey">权限提供者 Key。</param>
    /// <returns>指定组下的权限列表及授予状态。</returns>
    [HttpGet]
    [Route("by-group")]
    public override Task<GetPermissionListResultDto> GetByGroupAsync(string groupName, string providerName, string providerKey)
    {
        return PermissionAppService.GetByGroupAsync(groupName, providerName, providerKey);
    }

    /// <summary>
    /// 批量更新指定 Provider 的权限授予状态（授予或撤销）。
    /// </summary>
    /// <param name="providerName">权限提供者名称。</param>
    /// <param name="providerKey">权限提供者 Key。</param>
    /// <param name="input">包含待更新权限名及授予状态的 DTO。</param>
    [HttpPut]
    public override Task UpdateAsync(string providerName, string providerKey, UpdatePermissionsDto input)
    {
        return PermissionAppService.UpdateAsync(providerName, providerKey, input);
    }

    /// <summary>
    /// 获取可用的资源 Provider Key 查找服务列表，用于下拉选择。
    /// </summary>
    /// <param name="resourceName">资源名称。</param>
    /// <returns>可用的查找服务列表。</returns>
    [HttpGet("resource-provider-key-lookup-services")]
    public override Task<GetResourceProviderListResultDto> GetResourceProviderKeyLookupServicesAsync(string resourceName)
    {
        return PermissionAppService.GetResourceProviderKeyLookupServicesAsync(resourceName);
    }

    /// <summary>
    /// 搜索指定资源和服务的 Provider Key，支持模糊筛选和分页。
    /// </summary>
    /// <param name="resourceName">资源名称。</param>
    /// <param name="serviceName">查找服务名称。</param>
    /// <param name="filter">模糊筛选关键字。</param>
    /// <param name="page">分页页码。</param>
    /// <returns>匹配的 Provider Key 列表。</returns>
    [HttpGet("search-resource-provider-keys")]
    public override Task<SearchProviderKeyListResultDto> SearchResourceProviderKeyAsync(string resourceName, string serviceName, string filter, int page)
    {
        return PermissionAppService.SearchResourceProviderKeyAsync(resourceName, serviceName, filter, page);
    }

    /// <summary>
    /// 获取指定资源的所有权限定义列表。
    /// </summary>
    /// <param name="resourceName">资源名称。</param>
    /// <returns>资源的权限定义列表。</returns>
    [HttpGet("resource-definitions")]
    public override Task<GetResourcePermissionDefinitionListResultDto> GetResourceDefinitionsAsync(string resourceName)
    {
        return PermissionAppService.GetResourceDefinitionsAsync(resourceName);
    }

    /// <summary>
    /// 获取指定资源和 Key 的当前权限授予状态。
    /// </summary>
    /// <param name="resourceName">资源名称。</param>
    /// <param name="resourceKey">资源 Key。</param>
    /// <returns>该资源的权限列表及授予状态。</returns>
    [HttpGet]
    [Route("resource")]
    public override Task<GetResourcePermissionListResultDto> GetResourceAsync(string resourceName, string resourceKey)
    {
        return PermissionAppService.GetResourceAsync(resourceName, resourceKey);
    }

    /// <summary>
    /// 按 Provider 维度获取指定资源的权限授予详情。
    /// </summary>
    /// <param name="resourceName">资源名称。</param>
    /// <param name="resourceKey">资源 Key。</param>
    /// <param name="providerName">权限提供者名称。</param>
    /// <param name="providerKey">权限提供者 Key。</param>
    /// <returns>该资源在该 Provider 下的权限详情。</returns>
    [HttpGet]
    [Route("resource/by-provider")]
    public override Task<GetResourcePermissionWithProviderListResultDto> GetResourceByProviderAsync(string resourceName, string resourceKey, string providerName, string providerKey)
    {
        return PermissionAppService.GetResourceByProviderAsync(resourceName, resourceKey, providerName, providerKey);
    }

    /// <summary>
    /// 更新指定资源的权限授予状态。
    /// </summary>
    /// <param name="resourceName">资源名称。</param>
    /// <param name="resourceKey">资源 Key。</param>
    /// <param name="input">包含待更新权限的 DTO。</param>
    [HttpPut]
    [Route("resource")]
    public override Task UpdateResourceAsync(string resourceName, string resourceKey, UpdateResourcePermissionsDto input)
    {
        return PermissionAppService.UpdateResourceAsync(resourceName, resourceKey, input);
    }

    /// <summary>
    /// 删除指定资源在特定 Provider 下的所有权限授予。
    /// </summary>
    /// <param name="resourceName">资源名称。</param>
    /// <param name="resourceKey">资源 Key。</param>
    /// <param name="providerName">权限提供者名称。</param>
    /// <param name="providerKey">权限提供者 Key。</param>
    [HttpDelete]
    [Route("resource")]
    public override Task DeleteResourceAsync(string resourceName, string resourceKey, string providerName, string providerKey)
    {
        return PermissionAppService.DeleteResourceAsync(resourceName, resourceKey, providerName, providerKey);
    }
}
