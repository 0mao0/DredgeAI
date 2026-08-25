using Volo.Abp.Application.Services;

namespace DredgeAI.Permissions;

/// <summary>权限定义查询应用服务接口</summary>
/// <remarks>
/// 提供系统所有通过 PermissionDefinitionProvider 注册的权限组的树形结构查询能力。
/// 支持三个场景：纯定义树（无参数）、角色授权树（providerName=R）、用户授权树（providerName=U）。
/// </remarks>
public interface IPermissionDefinitionAppService : IApplicationService
{
    /// <summary>获取权限定义树</summary>
    /// <param name="providerName">
    /// 授权提供者名称，可选值：R（角色）、U（用户）。
    /// 传入时返回带 isGranted 字段的授权树。
    /// </param>
    /// <param name="providerKey">
    /// 授权提供者键，如角色名 admin 或用户 GUID。
    /// 仅当 providerName 同时传入时生效。
    /// </param>
    /// <returns>权限组树形结构列表</returns>
    Task<List<PermissionGroupTreeDto>> GetTreeAsync(string? providerName, string? providerKey);
}
