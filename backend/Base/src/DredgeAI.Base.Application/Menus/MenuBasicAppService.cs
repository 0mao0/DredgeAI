using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DredgeAI.MenuManagement;
using DredgeAI.Permissions;
using Volo.Abp;
using Volo.Abp.Authorization.Permissions;

namespace DredgeAI.MenuManagement;

public class MenuBasicAppService : DredgeAIBaseAppService, IMenuBasicAppService
{
    private readonly IPermissionDefinitionManager _permissionDefinitionManager;

    public MenuBasicAppService(IPermissionDefinitionManager permissionDefinitionManager)
    {
        _permissionDefinitionManager = permissionDefinitionManager;
    }

    public async Task<List<MenuPermissionGroupDto>> GetMenuPermissionsAsync()
    {
        var groups = await _permissionDefinitionManager.GetGroupsAsync();

        var result = new List<MenuPermissionGroupDto>();
        foreach (var group in groups)
        {
            var permissions = new List<MenuPermissionDto>();
            foreach (var rootPermission in group.Permissions)
            {
                CollectNonLeafPermissions(rootPermission, permissions);
            }

            if (permissions.Count == 0)
            {
                continue;
            }

            result.Add(new MenuPermissionGroupDto
            {
                Id = group.Name,
                Name = group.DisplayName?.Localize(StringLocalizerFactory) ?? group.Name,
                Code = group.Name,
                Children = permissions
            });
        }

        return result;
    }

    public async Task<List<NameValue<MenuType>>> GetMenuTypesAsync()
    {
        return await Task.FromResult(GetEnumNameValues<MenuType>());
    }

    public async Task<List<NameValue<RouteType>>> GetMenuRouteTypesAsync()
    {
        return await Task.FromResult(GetEnumNameValues<RouteType>());
    }

    public async Task<List<NameValue<IconType>>> GetMenuIconTypesAsync()
    {
        return await Task.FromResult(GetEnumNameValues<IconType>());
    }

    private List<NameValue<TEnum>> GetEnumNameValues<TEnum>() where TEnum : Enum
    {
        return Enum.GetValues(typeof(TEnum))
            .Cast<TEnum>()
            .Select(e => new NameValue<TEnum>(
                typeof(TEnum).GetField(e.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? e.ToString(),
                e
            )).ToList();
    }

    private void CollectNonLeafPermissions(PermissionDefinition permission, List<MenuPermissionDto> result)
    {
        if (permission.Children.Count == 0)
        {
            return;
        }

        result.Add(new MenuPermissionDto
        {
            Id = permission.Name,
            Name = permission.DisplayName?.Localize(StringLocalizerFactory) ?? permission.Name,
            Code = permission.Name,
            Children = []
        });

        foreach (var child in permission.Children)
        {
            CollectNonLeafPermissions(child, result);
        }
    }
}
