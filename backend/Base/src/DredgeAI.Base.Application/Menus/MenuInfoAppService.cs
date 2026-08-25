using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DredgeAI.MenuManagement;
using DredgeAI.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;

namespace DredgeAI.MenuManagement;

public class MenuInfoAppService : DredgeAIBaseAppService, IMenuInfoAppService
{
    private readonly IRepository<MenuInfo, Guid> _menuInfoRepo;
    private readonly MenuInfoManager _menuInfoManager;
    private readonly IPermissionChecker _permissionChecker;

    public MenuInfoAppService(
        IRepository<MenuInfo, Guid> menuInfoRepo,
        MenuInfoManager menuInfoManager,
        IPermissionChecker permissionChecker)
    {
        _menuInfoRepo = menuInfoRepo;
        _menuInfoManager = menuInfoManager;
        _permissionChecker = permissionChecker;
    }

    public async Task<MenuInfoDto> GetAsync(Guid id)
    {
        var entity = await _menuInfoRepo.GetAsync(id);
        return ObjectMapper.Map<MenuInfo, MenuInfoDto>(entity);
    }

    public async Task<PagedResultDto<MenuInfoDto>> GetListAsync(GetMenuInfoListInput input)
    {
        var query = await _menuInfoRepo.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Name))
        {
            query = query.Where(m => m.Name.Contains(input.Name!));
        }

        if (input.Type.HasValue)
        {
            query = query.Where(m => m.Type == input.Type!.Value);
        }

        if (input.IsEnabled.HasValue)
        {
            query = query.Where(m => m.IsEnabled == input.IsEnabled!.Value);
        }

        var totalCount = await AsyncExecuter.CountAsync(query);

        var items = await AsyncExecuter.ToListAsync(
            query.OrderBy(m => m.SortId).ThenBy(m => m.Title)
                 .Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<MenuInfoDto>(
            totalCount,
            items.Select(m => ObjectMapper.Map<MenuInfo, MenuInfoDto>(m)).ToList());
    }

    public async Task<MenuInfoDto> CreateAsync(MenuInfoCreateUpdateDto input)
    {
        if (input.ParentId.HasValue)
        {
            await _menuInfoManager.EnsureParentExistsAsync(input.ParentId.Value);
        }

        await _menuInfoManager.EnsureNameUniqueAsync(input.Name, input.ParentId, null);

        var entity = new MenuInfo(
            GuidGenerator.Create(),
            input.ParentId,
            input.Type,
            input.Name,
            input.Title,
            input.ComponentPath,
            null,
            input.RedirectPath,
            input.Icon,
            input.IconType,
            input.RouteType,
            input.PermissionCode,
            input.SortId,
            input.IsEnabled,
            input.IsCache,
            input.IsFixed,
            input.IsHidden,
            false,
            input.Remark);

        // Auto-generate RoutePath from Name + parent hierarchy
        var routePath = await GenerateRoutePathAsync(input.Name, input.ParentId);
        entity.SetRoutePath(routePath);

        await _menuInfoRepo.InsertAsync(entity);

        return ObjectMapper.Map<MenuInfo, MenuInfoDto>(entity);
    }

    public async Task<MenuInfoDto> UpdateAsync(Guid id, MenuInfoCreateUpdateDto input)
    {
        await _menuInfoManager.ValidateNoCircularReferenceAsync(id, input.ParentId);

        var entity = await _menuInfoRepo.GetAsync(id);

        await _menuInfoManager.ValidateNotStaticAsync(entity);

        if (input.ParentId != entity.ParentId)
        {
            if (input.ParentId.HasValue)
            {
                await _menuInfoManager.EnsureParentExistsAsync(input.ParentId.Value);
            }
        }

        if (input.Name != entity.Name)
        {
            await _menuInfoManager.EnsureNameUniqueAsync(input.Name, input.ParentId, id);
        }

        var nameChanged = input.Name != entity.Name;
        var parentChanged = input.ParentId != entity.ParentId;

        entity.SetParentId(input.ParentId);
        entity.SetType(input.Type);
        entity.SetName(input.Name);
        entity.SetTitle(input.Title);
        entity.SetComponentPath(input.ComponentPath);
        entity.SetRedirectPath(input.RedirectPath);
        entity.SetIcon(input.Icon);
        entity.SetIconType(input.IconType);
        entity.SetRouteType(input.RouteType);
        entity.SetPermissionCode(input.PermissionCode);
        entity.SetSortId(input.SortId);
        entity.SetIsEnabled(input.IsEnabled);
        entity.SetIsCache(input.IsCache);
        entity.SetIsFixed(input.IsFixed);
        entity.SetIsHidden(input.IsHidden);
        entity.SetRemark(input.Remark);

        if (nameChanged || parentChanged)
        {
            var newRoutePath = await GenerateRoutePathAsync(input.Name, input.ParentId);
            entity.SetRoutePath(newRoutePath);
            await CascadeRoutePathAsync(entity);
        }

        await _menuInfoRepo.UpdateAsync(entity);

        return ObjectMapper.Map<MenuInfo, MenuInfoDto>(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _menuInfoRepo.GetAsync(id);

        await _menuInfoManager.ValidateNotStaticAsync(entity);

        var childQuery = await _menuInfoRepo.GetQueryableAsync();
        var hasChildren = await AsyncExecuter.AnyAsync(
            childQuery.Where(m => m.ParentId == id));

        if (hasChildren)
        {
            throw new BusinessException("DredgeAIBase:MenuCannotDeleteWithChildren");
        }

        await _menuInfoRepo.DeleteAsync(entity);
    }

    public async Task<List<MenuInfoDto>> GetTreeAsync(GetMenuInfoTreeInput input)
    {
        var query = await _menuInfoRepo.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Name))
        {
            query = query.Where(m => m.Name.Contains(input.Name!));
        }

        if (input.Type.HasValue)
        {
            query = query.Where(m => m.Type == input.Type!.Value);
        }

        if (input.IsEnabled.HasValue)
        {
            query = query.Where(m => m.IsEnabled == input.IsEnabled!.Value);
        }

        var allMenus = await AsyncExecuter.ToListAsync(query);

        return BuildTree(allMenus, null);
    }

    public async Task<List<MenuTreeNodeDto>> GetCurrentUserPermittedTreeAsync(GetMenuInfoTreeInput input)
    {
        var query = await _menuInfoRepo.GetQueryableAsync();

        query = query.Where(m => m.Type != MenuType.Button
            && m.IsEnabled
            && !m.IsHidden);

        if (!string.IsNullOrWhiteSpace(input.Name))
        {
            query = query.Where(m => m.Name.Contains(input.Name!));
        }

        if (input.Type.HasValue)
        {
            query = query.Where(m => m.Type == input.Type!.Value);
        }

        if (input.IsEnabled.HasValue)
        {
            query = query.Where(m => m.IsEnabled == input.IsEnabled!.Value);
        }

        var allMenus = await AsyncExecuter.ToListAsync(query);

        var tree = BuildTreeNode(allMenus, null);

        return await PruneTreeNodeByPermissionAsync(tree);
    }

    private List<MenuInfoDto> BuildTree(List<MenuInfo> allMenus, Guid? parentId)
    {
        var lookup = allMenus.ToLookup(m => m.ParentId);
        return BuildTreeFromLookup(lookup, parentId);
    }

    private List<MenuInfoDto> BuildTreeFromLookup(ILookup<Guid?, MenuInfo> lookup, Guid? parentId)
    {
        return lookup[parentId].OrderBy(m => m.SortId).ThenBy(m => m.Title)
            .Select(m =>
            {
                var dto = ObjectMapper.Map<MenuInfo, MenuInfoDto>(m);
                dto.Children = BuildTreeFromLookup(lookup, m.Id);
                return dto;
            }).ToList();
    }

    private List<MenuTreeNodeDto> BuildTreeNode(List<MenuInfo> allMenus, Guid? parentId)
    {
        var lookup = allMenus.ToLookup(m => m.ParentId);
        return BuildTreeNodeFromLookup(lookup, parentId);
    }

    private List<MenuTreeNodeDto> BuildTreeNodeFromLookup(ILookup<Guid?, MenuInfo> lookup, Guid? parentId)
    {
        return lookup[parentId].OrderBy(m => m.SortId).ThenBy(m => m.Title)
            .Select(m =>
            {
                var dto = ObjectMapper.Map<MenuInfo, MenuTreeNodeDto>(m);
                dto.Children = BuildTreeNodeFromLookup(lookup, m.Id);
                return dto;
            }).ToList();
    }

    private async Task<List<MenuTreeNodeDto>> PruneTreeNodeByPermissionAsync(List<MenuTreeNodeDto> nodes)
    {
        var result = new List<MenuTreeNodeDto>();
        foreach (var node in nodes)
        {
            if (string.IsNullOrWhiteSpace(node.PermissionCode)
                || await _permissionChecker.IsGrantedAsync(node.PermissionCode))
            {
                node.Children = (await PruneTreeNodeByPermissionAsync(node.Children)).ToList();
                result.Add(node);
            }
        }
        return result;
    }

    private async Task<string> GenerateRoutePathAsync(string name, Guid? parentId)
    {
        var kebabName =name.ToKebabCase();
        if (parentId.HasValue)
        {
            var parent = await _menuInfoRepo.GetAsync(parentId.Value);
            return (parent.RoutePath ?? "/") + "/" + kebabName;
        }
        return "/" + kebabName;
    }

    private async Task CascadeRoutePathAsync(MenuInfo updatedMenuInfo)
    {
        var query = await _menuInfoRepo.GetQueryableAsync();
        var allMenus = await AsyncExecuter.ToListAsync(query);
        var lookup = allMenus.ToLookup(m => m.ParentId);
        var updatedMenus = new List<MenuInfo>();

        CascadeRoutePathRecursive(lookup, updatedMenuInfo, updatedMenus);

        foreach (var menu in updatedMenus)
        {
            await _menuInfoRepo.UpdateAsync(menu);
        }
    }

    private void CascadeRoutePathRecursive(ILookup<Guid?, MenuInfo> lookup, MenuInfo parent, List<MenuInfo> updatedMenus)
    {
        foreach (var child in lookup[parent.Id])
        {
            var newPath = (parent.RoutePath ?? "/") + "/" + child.Name.ToKebabCase();
            child.SetRoutePath(newPath);
            updatedMenus.Add(child);
            CascadeRoutePathRecursive(lookup, child, updatedMenus);
        }
    }
   
}
