namespace DredgeAI.UserManagement;

/// <summary>更新用户请求 DTO</summary>
public class UpdateUserDto
{
    /// <summary>姓名，不传表示保持不变</summary>
    public string? Name { get; set; }

    /// <summary>手机号，不传表示保持不变</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>分配的角色名列表，不传表示保持不变</summary>
    public List<string>? RoleNames { get; set; }

    /// <summary>分配的组织 ID 列表，不传表示保持不变</summary>
    public List<Guid>? OrganizationIds { get; set; }

    /// <summary>过期时间，不传表示保持不变</summary>
    public DateTime? ExpireTime { get; set; }
}
