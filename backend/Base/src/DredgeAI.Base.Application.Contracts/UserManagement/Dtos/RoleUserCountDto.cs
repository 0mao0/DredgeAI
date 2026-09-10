namespace DredgeAI.UserManagement;

/// <summary>角色用户数量统计 DTO</summary>
public class RoleUserCountDto
{
    /// <summary>角色名称</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>角色内用户数量</summary>
    public int UserCount { get; set; }
}
