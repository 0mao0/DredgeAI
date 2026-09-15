using Volo.Abp.PermissionManagement;

namespace DredgeAI.Permissions;

/// <summary>内部集成服务实现：资源授权查询直通 <see cref="IResourcePermissionGrantRepository"/>，无权限门槛，仅供集群内服务调用。</summary>
public class InternalPermissionQueryAppService : DredgeAIBaseAppService, IInternalPermissionQueryAppService
{
    private readonly IResourcePermissionGrantRepository _resourcePermissionGrantRepository;

    public InternalPermissionQueryAppService(IResourcePermissionGrantRepository resourcePermissionGrantRepository)
    {
        _resourcePermissionGrantRepository = resourcePermissionGrantRepository;
    }

    public virtual async Task<List<ResourcePermissionGrantItemDto>> GetResourceGrantsAsync(string resourceName, string permissionName)
    {
        var grants = await _resourcePermissionGrantRepository.GetResourceKeys(resourceName, permissionName);
        return grants
            .Select(g => new ResourcePermissionGrantItemDto
            {
                ProviderName = g.ProviderName,
                ProviderKey = g.ProviderKey,
                ResourceKey = g.ResourceKey
            })
            .ToList();
    }
}
