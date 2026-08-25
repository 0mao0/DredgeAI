namespace DredgeAI.MenuManagement;

/// <summary>菜单树查询输入 DTO</summary>
/// <remarks>树查询返回完整层级结构，不支持分页。仅支持按名称关键词、类型和启用状态筛选</remarks>
public class GetMenuInfoTreeInput
{
    /// <summary>搜索关键字，按菜单名称模糊匹配</summary>
    public string? Name { get; set; }

    /// <summary>菜单类型筛选，不传则查询全部类型</summary>
    public MenuType? Type { get; set; }

    /// <summary>启用状态筛选，不传则查询全部状态</summary>
    public bool? IsEnabled { get; set; }
}
