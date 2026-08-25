using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DredgeAI.MenuManagement;
using DredgeAI.Permissions;
using Volo.Abp;

namespace DredgeAI.Controllers;

/// <summary>菜单基础数据接口</summary>
/// <remarks>提供菜单相关枚举字典的查询能力，用于前端下拉框数据源</remarks>
[Authorize]
[Route("api/base/menu-basics")]
[RemoteService(Name = DredgeAIBaseRemoteServiceConsts.RemoteServiceName)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("菜单管理")]
public class MenuBasicController : DredgeAIBaseController, IMenuBasicAppService
{
    private readonly IMenuBasicAppService _service;

    public MenuBasicController(IMenuBasicAppService service)
    {
        _service = service;
    }

    /// <summary>获取菜单类型枚举列表</summary>
    /// <returns>菜单类型键值对列表，包含目录（0）、菜单（1）、按钮（2）三种类型</returns>
    [HttpGet("menu-types")]
    [Authorize(DredgeAIBasePermissions.Menus.Default)]
    public Task<List<NameValue<MenuType>>> GetMenuTypesAsync()
        => _service.GetMenuTypesAsync();

    /// <summary>获取路由类型枚举列表</summary>
    /// <returns>路由类型键值对列表，包含默认（0）、内嵌iframe（1）、新窗口（2）三种类型</returns>
    [HttpGet("menu-route-types")]
    [Authorize(DredgeAIBasePermissions.Menus.Default)]
    public Task<List<NameValue<RouteType>>> GetMenuRouteTypesAsync()
        => _service.GetMenuRouteTypesAsync();

    /// <summary>获取图标类型枚举列表</summary>
    /// <returns>图标类型键值对列表，包含阿里图标（0）、Element图标（1）、FontAwesome图标（2）三种类型</returns>
    [HttpGet("menu-icon-types")]
    [Authorize(DredgeAIBasePermissions.Menus.Default)]
    public Task<List<NameValue<IconType>>> GetMenuIconTypesAsync()
        => _service.GetMenuIconTypesAsync();

    /// <summary>获取菜单可选权限列表</summary>
    /// <returns>所有权限组中可作为菜单/目录权限编码绑定的非叶子权限节点列表</returns>
    [HttpGet("menu-permissions")]
    [Authorize(DredgeAIBasePermissions.Menus.Default)]
    public Task<List<MenuPermissionGroupDto>> GetMenuPermissionsAsync()
        => _service.GetMenuPermissionsAsync();
}
