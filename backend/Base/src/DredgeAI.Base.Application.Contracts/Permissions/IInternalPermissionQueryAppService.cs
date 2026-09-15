using Volo.Abp;
using Volo.Abp.Application.Services;

namespace DredgeAI.Permissions;

/// <summary>内部集成服务：资源授权查询。供集群内服务（BidCompare）经动态 C# 客户端代理调用；/integration-api 前缀，网关无该路由不外露。绕过管理权限门槛返回原始授权条目，调用方自行按当前用户过滤。</summary>
[IntegrationService]
public interface IInternalPermissionQueryAppService : IApplicationService
{
    /// <summary>按资源名 + 权限名返回全部授权条目（IResourcePermissionGrantRepository.GetResourceKeys 直通）。</summary>
    Task<List<ResourcePermissionGrantItemDto>> GetResourceGrantsAsync(string resourceName, string permissionName);
}
