using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace DredgeAI.MenuManagement;

/// <summary>菜单创建/更新请求 DTO</summary>
/// <remarks>创建和更新共用同一个 DTO，Id 由路由参数提供</remarks>
public class MenuInfoCreateUpdateDto
{
    /// <summary>父菜单 ID，为 null 表示根节点</summary>
    public Guid? ParentId { get; set; }

    /// <summary>菜单类型：0=目录、1=菜单、2=按钮（必填）</summary>
    [Required]
    public MenuType Type { get; set; }

    /// <summary>菜单名称，同层级下必须唯一（必填）</summary>
    [Required]
    [DynamicStringLength(typeof(MenuInfoConsts), nameof(MenuInfoConsts.MaxNameLength))]
    public string Name { get; set; } = string.Empty;

    /// <summary>菜单标题，用于界面显示（必填）</summary>
    [Required]
    [DynamicStringLength(typeof(MenuInfoConsts), nameof(MenuInfoConsts.MaxTitleLength))]
    public string Title { get; set; } = string.Empty;

    /// <summary>前端组件路径</summary>
    [DynamicStringLength(typeof(MenuInfoConsts), nameof(MenuInfoConsts.MaxComponentPathLength))]
    public string? ComponentPath { get; set; }

    /// <summary>重定向路径</summary>
    [DynamicStringLength(typeof(MenuInfoConsts), nameof(MenuInfoConsts.MaxRedirectPathLength))]
    public string? RedirectPath { get; set; }

    /// <summary>图标名称</summary>
    [DynamicStringLength(typeof(MenuInfoConsts), nameof(MenuInfoConsts.MaxIconLength))]
    public string? Icon { get; set; }

    /// <summary>图标类型</summary>
    public IconType IconType { get; set; }

    /// <summary>路由行为</summary>
    public RouteType RouteType { get; set; }

    /// <summary>权限标识码（必填）</summary>
    [Required]
    [DynamicStringLength(typeof(MenuInfoConsts), nameof(MenuInfoConsts.MaxPermissionCodeLength))]
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>排序序号，数值越小越靠前，默认 100</summary>
    [DynamicRange(typeof(MenuInfoConsts), typeof(int), nameof(MenuInfoConsts.MinSort), nameof(MenuInfoConsts.MaxSort))]
    public uint SortId { get; set; } = 100;

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>是否缓存</summary>
    public bool IsCache { get; set; }

    /// <summary>是否固定在标签栏</summary>
    public bool IsFixed { get; set; }

    /// <summary>是否隐藏菜单</summary>
    public bool IsHidden { get; set; }

    /// <summary>备注说明</summary>
    [DynamicStringLength(typeof(MenuInfoConsts), nameof(MenuInfoConsts.MaxRemarkLength))]
    public string? Remark { get; set; }
}
