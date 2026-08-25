namespace DredgeAI.Permissions;

/// <summary>权限组树形节点 DTO</summary>
/// <remarks>包含权限组名称、显示名及该组下的根权限列表</remarks>
public class PermissionGroupTreeDto
{
    /// <summary>权限组名称（唯一标识）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>权限组显示名称</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>该组下的根权限列表</summary>
    public List<PermissionTreeDto> Permissions { get; set; } = [];
}
