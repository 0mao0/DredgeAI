using System.Net;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.AuditLogs;

/// <summary>审计日志分页查询输入</summary>
public class GetAuditLogListInput : PagedAndSortedResultRequestDto
{
    /// <summary>开始时间</summary>
    public DateTime? StartTime { get; set; }

    /// <summary>结束时间</summary>
    public DateTime? EndTime { get; set; }

    /// <summary>用户 ID</summary>
    public Guid? UserId { get; set; }

    /// <summary>用户名关键字</summary>
    public string? UserName { get; set; }

    /// <summary>HTTP 方法</summary>
    public string? HttpMethod { get; set; }

    /// <summary>URL 关键字</summary>
    public string? Url { get; set; }

    /// <summary>HTTP 状态码</summary>
    public HttpStatusCode? HttpStatusCode { get; set; }

    /// <summary>是否只查询有异常的日志</summary>
    public bool? HasException { get; set; }
}
