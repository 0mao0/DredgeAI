namespace DredgeAI.AuditLogs;

/// <summary>审计日志操作人信息</summary>
public class OperatorInfoDto
{
    /// <summary>用户 ID</summary>
    public Guid? UserId { get; set; }

    /// <summary>用户名</summary>
    public string? UserName { get; set; }

    /// <summary>显示名称（Name + Surname 拼接）</summary>
    public string? DisplayName { get; set; }

    /// <summary>角色名称列表</summary>
    public List<string> RoleNames { get; set; } = [];

    /// <summary>所属组织机构名称列表</summary>
    public List<string> OrganizationUnits { get; set; } = [];
}
