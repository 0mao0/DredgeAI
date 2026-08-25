using Volo.Abp.Application.Dtos;

namespace DredgeAI.UserManagement;

/// <summary>用户 DTO</summary>
public class UserDto : EntityDto<Guid>
{
    /// <summary>用户名</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>姓名</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>手机号</summary>
    public string? PhoneNumber { get; set; }

    /// <summary>邮箱</summary>
    public string? Email { get; set; }

    /// <summary>是否启用</summary>
    public bool IsActive { get; set; }

    /// <summary>过期时间</summary>
    public DateTime? ExpireTime { get; set; }

    /// <summary>所属组织单位列表</summary>
    public List<OrganizationUnitBriefDto> OrganizationUnits { get; set; } = [];

    /// <summary>角色名列表</summary>
    public List<string> RoleNames { get; set; } = [];

    /// <summary>创建时间</summary>
    public DateTime CreationTime { get; set; }
}
