using Volo.Abp.Application.Dtos;

namespace DredgeAI.SecurityLogs;

/// <summary>安全日志分页查询输入</summary>
public class GetSecurityLogListInput : PagedAndSortedResultRequestDto
{
    /// <summary>开始时间</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>结束时间</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>用户 ID</summary>
    public Guid? UserId { get; set; }

    /// <summary>用户名关键字</summary>
    public string? UserName { get; set; }

    /// <summary>动作</summary>
    public string? Action { get; set; }

    /// <summary>身份</summary>
    public string? Identity { get; set; }

    /// <summary>客户端 IP 地址</summary>
    public string? ClientIpAddress { get; set; }
}
