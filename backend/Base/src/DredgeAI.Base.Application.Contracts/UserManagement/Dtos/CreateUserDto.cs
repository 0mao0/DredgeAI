using System.ComponentModel.DataAnnotations;

namespace DredgeAI.UserManagement;

/// <summary>创建用户请求 DTO</summary>
public class CreateUserDto
{
    /// <summary>用户名（必填）</summary>
    [Required]
    public string UserName { get; set; } = string.Empty;

    /// <summary>姓名（必填）</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>手机号（必填，11 位数字）</summary>
    [Required]
    [RegularExpression(@"^1[3-9]\d{9}$", ErrorMessage = "手机号格式不正确")]
    public string PhoneNumber { get; set; } = string.Empty;

    /// <summary>密码（必填）</summary>
    [Required]
    public string Password { get; set; } = string.Empty;

    /// <summary>分配的角色名列表</summary>
    public List<string>? RoleNames { get; set; }

    /// <summary>分配的组织 ID 列表</summary>
    public List<Guid>? OrganizationIds { get; set; }

    /// <summary>过期时间</summary>
    public DateTime? ExpireTime { get; set; }
}
