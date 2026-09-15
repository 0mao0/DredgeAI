using Volo.Abp.PermissionManagement;

namespace DredgeAI.Permissions;

/// <summary>
/// 权限管理应用服务（扩展 ABP 内置 <see cref="IPermissionAppService"/>）。
/// 新增批量资源权限更新：对同一资源的多个 Key 应用同一组授权。
/// </summary>
public interface IMyPermissionAppService : IPermissionAppService
{
    /// <summary>批量更新同一资源下多个 Key 的权限授予状态。</summary>
    /// <param name="resourceName">资源名称。</param>
    /// <param name="resourceKeys">资源 Key 列表；为空或 null 时不做任何操作。</param>
    /// <param name="input">授权内容（ProviderName/ProviderKey/授予的权限名列表），对每个 Key 全量覆盖。</param>
    Task UpdateResourceAsync(string resourceName, List<string> resourceKeys, UpdateResourcePermissionsDto input);
}
