using Volo.Abp.Authorization.Permissions;
using Volo.Abp.PermissionManagement;

namespace DredgeAI.Permissions;

/// <summary>权限定义查询应用服务</summary>
/// <remarks>
/// 通过 ABP 的 IPermissionDefinitionManager 获取系统所有权限定义的树形结构。
/// 当传入 providerName 和 providerKey 时，通过 IPermissionGrantRepository 查询授权记录并填充 IsGranted 字段。
/// </remarks>
public class PermissionDefinitionAppService : DredgeAIBaseAppService, IPermissionDefinitionAppService
{
    private readonly IPermissionDefinitionManager _permissionDefinitionManager;
    private readonly IPermissionGrantRepository _permissionGrantRepository;

    public PermissionDefinitionAppService(
        IPermissionDefinitionManager permissionDefinitionManager,
        IPermissionGrantRepository permissionGrantRepository)
    {
        _permissionDefinitionManager = permissionDefinitionManager;
        _permissionGrantRepository = permissionGrantRepository;
    }

    public async Task<List<PermissionGroupTreeDto>> GetTreeAsync(string? providerName, string? providerKey)
    {
        var groups = await _permissionDefinitionManager.GetGroupsAsync();

        HashSet<string>? grantedNames = null;
        if (!string.IsNullOrWhiteSpace(providerName) && !string.IsNullOrWhiteSpace(providerKey))
        {
            var grants = await _permissionGrantRepository.GetListAsync(providerName, providerKey);
            grantedNames = new HashSet<string>(grants.Select(g => g.Name));
        }

        var result = new List<PermissionGroupTreeDto>();
        foreach (var group in groups)
        {
            var groupDto = new PermissionGroupTreeDto
            {
                Name = group.Name,
                DisplayName = group.DisplayName?.Localize(StringLocalizerFactory) ?? group.Name,
                Permissions = []
            };

            foreach (var rootPermission in group.Permissions)
            {
                groupDto.Permissions.Add(BuildPermissionNode(rootPermission, grantedNames));
            }

            result.Add(groupDto);
        }

        return result;
    }

    private PermissionTreeDto BuildPermissionNode(
        PermissionDefinition permission,
        HashSet<string>? grantedNames)
    {
        var dto = new PermissionTreeDto
        {
            Name = permission.Name,
            DisplayName = permission.DisplayName?.Localize(StringLocalizerFactory) ?? permission.Name,
            ParentName = permission.Parent?.Name,
            IsGranted = grantedNames?.Contains(permission.Name),
            Children = []
        };

        foreach (var child in permission.Children)
        {
            dto.Children.Add(BuildPermissionNode(child, grantedNames));
        }

        return dto;
    }
}
