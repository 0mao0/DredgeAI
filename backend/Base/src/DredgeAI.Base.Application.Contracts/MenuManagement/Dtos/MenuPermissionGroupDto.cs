namespace DredgeAI.MenuManagement;

/// <summary>菜单可选权限组 DTO</summary>
/// <remarks>包含权限组名称、显示名及该组下可作为菜单绑定的非叶子权限列表</remarks>
public class MenuPermissionGroupDto
{
    /// <summary>权限组标识（权限组名称）</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>权限组显示名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>权限组编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>该组下可作为菜单绑定的非叶子权限列表</summary>
    public List<MenuPermissionDto> Children { get; set; } = [];
}
