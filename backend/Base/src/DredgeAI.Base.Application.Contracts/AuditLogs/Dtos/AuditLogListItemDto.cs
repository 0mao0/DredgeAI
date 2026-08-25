using Volo.Abp.Application.Dtos;

namespace DredgeAI.AuditLogs;

/// <summary>审计日志列表项 DTO</summary>
public class AuditLogListItemDto : EntityDto<Guid>
{
    /// <summary>应用名称</summary>
    public string? ApplicationName { get; set; }

    /// <summary>操作人信息</summary>
    public OperatorInfoDto Operator { get; set; } = new();

    /// <summary>执行时间</summary>
    public DateTime ExecutionTime { get; set; }

    /// <summary>执行耗时（毫秒）</summary>
    public int ExecutionDuration { get; set; }

    /// <summary>客户端 IP 地址</summary>
    public string? ClientIpAddress { get; set; }

    /// <summary>客户端名称</summary>
    public string? ClientName { get; set; }

    /// <summary>客户端 ID</summary>
    public string? ClientId { get; set; }

    /// <summary>CorrelationId</summary>
    public string? CorrelationId { get; set; }

    /// <summary>浏览器信息</summary>
    public string? BrowserInfo { get; set; }

    /// <summary>HTTP 方法</summary>
    public string? HttpMethod { get; set; }

    /// <summary>请求 URL</summary>
    public string? Url { get; set; }

    /// <summary>HTTP 状态码</summary>
    public int? HttpStatusCode { get; set; }

    /// <summary>异常信息</summary>
    public string? Exceptions { get; set; }

    /// <summary>备注</summary>
    public string? Comments { get; set; }
}
