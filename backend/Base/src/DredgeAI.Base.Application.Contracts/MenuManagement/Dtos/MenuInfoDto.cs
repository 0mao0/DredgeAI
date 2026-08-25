using Volo.Abp.Application.Dtos;

namespace DredgeAI.MenuManagement;

/// <summary>菜单 DTO</summary>
/// <remarks>支持树形结构，Children 含递归子菜单列表</remarks>
public class MenuInfoDto : ExtensibleAuditedEntityDto<Guid>
{
    /// <summary>父菜单 ID，为 null 表示根节点</summary>
    public Guid? ParentId { get; set; }

    /// <summary>菜单类型：0=目录、1=菜单、2=按钮</summary>
    public MenuType Type { get; set; }

    /// <summary>菜单名称（唯一标识）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>菜单标题（显示用）</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>前端组件路径</summary>
    public string? ComponentPath { get; set; }

    /// <summary>前端路由路径</summary>
    public string? RoutePath { get; set; }

    /// <summary>重定向路径</summary>
    public string? RedirectPath { get; set; }

    /// <summary>图标名称</summary>
    public string? Icon { get; set; }

    /// <summary>图标类型：0=阿里图标、1=Element图标、2=FontAwesome图标</summary>
    public IconType IconType { get; set; }

    /// <summary>路由行为：0=默认、1=内嵌iframe、2=新窗口</summary>
    public RouteType RouteType { get; set; }

    /// <summary>权限标识码</summary>
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>排序序号，数值越小越靠前</summary>
    public uint SortId { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }

    /// <summary>是否缓存</summary>
    public bool IsCache { get; set; }

    /// <summary>是否固定在标签栏</summary>
    public bool IsFixed { get; set; }

    /// <summary>是否隐藏菜单</summary>
    public bool IsHidden { get; set; }

    /// <summary>是否为系统菜单（不可修改和删除）</summary>
    public bool IsStatic { get; set; }

    /// <summary>备注说明</summary>
    public string? Remark { get; set; }

    /// <summary>子级菜单列表（递归）</summary>
    public List<MenuInfoDto> Children { get; set; } = [];
}
