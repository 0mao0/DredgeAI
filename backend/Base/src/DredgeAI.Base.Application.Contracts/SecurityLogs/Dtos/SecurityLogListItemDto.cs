using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.SecurityLogs;

/// <summary>安全日志列表项 DTO</summary>
public class SecurityLogListItemDto : EntityDto<Guid>
{
    /// <summary>应用名称</summary>
    public string? ApplicationName { get; set; }

    /// <summary>身份</summary>
    public string? Identity { get; set; }

    /// <summary>动作</summary>
    public string? Action { get; set; }

    /// <summary>用户 ID</summary>
    public Guid? UserId { get; set; }

    /// <summary>用户名</summary>
    public string? UserName { get; set; }

    /// <summary>显示名称（Name + Surname 拼接）</summary>
    public string? DisplayName { get; set; }

    /// <summary>角色名列表</summary>
    public List<string> RoleNames { get; set; } = new();

    /// <summary>组织机构名列表</summary>
    public List<string> OrganizationUnitNames { get; set; } = new();

    /// <summary>租户名</summary>
    public string? TenantName { get; set; }

    /// <summary>客户端 ID</summary>
    public string? ClientId { get; set; }

    /// <summary>客户端 IP 地址</summary>
    public string? ClientIpAddress { get; set; }

    /// <summary>浏览器信息</summary>
    public string? BrowserInfo { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreationTime { get; set; }
}
