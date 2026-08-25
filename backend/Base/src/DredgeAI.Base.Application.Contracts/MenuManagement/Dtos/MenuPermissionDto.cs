namespace DredgeAI.MenuManagement;

/// <summary>菜单可选权限节点 DTO</summary>
/// <remarks>表示权限树中的非叶子节点，用于菜单/目录权限编码绑定</remarks>
public class MenuPermissionDto
{
    /// <summary>权限树节点键（权限名称）</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>权限显示名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>权限编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>子权限列表（菜单权限选项不返回子权限，固定为空数组）</summary>
    public List<MenuPermissionDto> Children { get; set; } = [];
}
