namespace DredgeAI.Permissions;

/// <summary>权限树形节点 DTO</summary>
/// <remarks>
/// 支持多级嵌套树形结构，Children 含递归子权限列表。
/// 当 providerName/providerKey 均传入时，IsGranted 根据授权记录填充；
/// 否则 IsGranted 为 null 表示无授权上下文。
/// </remarks>
public class PermissionTreeDto
{
    /// <summary>权限名称（唯一标识）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>权限显示名称</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>父权限名称，为 null 表示根节点</summary>
    public string? ParentName { get; set; }

    /// <summary>是否已授权，null 表示无授权上下文</summary>
    public bool? IsGranted { get; set; }

    /// <summary>子级权限列表（递归）</summary>
    public List<PermissionTreeDto> Children { get; set; } = [];
}
