using Volo.Abp;

namespace DredgeAI.MenuManagement;

/// <summary>菜单基础数据应用服务接口</summary>
/// <remarks>提供菜单相关枚举字典的查询能力，用于前端下拉框数据源</remarks>
public interface IMenuBasicAppService
{
    /// <summary>获取菜单类型枚举列表</summary>
    /// <returns>菜单类型键值对列表（目录/菜单/按钮）</returns>
    Task<List<NameValue<MenuType>>> GetMenuTypesAsync();

    /// <summary>获取路由类型枚举列表</summary>
    /// <returns>路由类型键值对列表（默认/内嵌iframe/新窗口）</returns>
    Task<List<NameValue<RouteType>>> GetMenuRouteTypesAsync();

    /// <summary>获取图标类型枚举列表</summary>
    /// <returns>图标类型键值对列表（阿里图标/Element图标/FontAwesome图标）</returns>
    Task<List<NameValue<IconType>>> GetMenuIconTypesAsync();

    /// <summary>获取菜单可选权限列表</summary>
    /// <returns>所有权限组中可作为菜单/目录权限编码绑定的非叶子权限节点列表</returns>
    Task<List<MenuPermissionGroupDto>> GetMenuPermissionsAsync();
}
