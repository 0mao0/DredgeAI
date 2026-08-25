using System.Collections.Generic;
using Volo.Abp.ObjectExtending;

namespace DredgeAI.MenuManagement;

/// <summary>
/// 菜单树节点 DTO（用于前端路由与导航渲染）
/// </summary>
/// <remarks>
/// 属性设计参考 Shiw.Menu.MenuDto，包含前端路由所需的全部字段。
/// 与 MenuInfoDto 的区别：本 DTO 专注于树形导航结构，不包含审计字段。
/// </remarks>
public class MenuTreeNodeDto : ExtensibleObject
{
    /// <summary>
    /// 关键字【在整个菜单定义中保持唯一】
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 路径
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 跳转Url
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// 路由名【在整个菜单定义中保持唯一】
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 菜单类型
    /// </summary>
    public MenuType MenuType { get; set; }

    /// <summary>
    /// 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 图标
    /// </summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// 权限编码
    /// </summary>
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>
    /// 组件名
    /// </summary>
    public string Component { get; set; } = string.Empty;

    /// <summary>
    /// 重定向地址
    /// </summary>
    public string Redirect { get; set; } = string.Empty;

    /// <summary>
    /// 是否子节点
    /// </summary>
    public bool IsLeaf => Children.Count == 0;

    /// <summary>
    /// 子菜单
    /// </summary>
    public virtual List<MenuTreeNodeDto> Children { get; set; } = new List<MenuTreeNodeDto>();
}
