using System;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.Gateway.Proxying;

public class ProxyRouteDto : AuditedEntityDto<Guid>
{
    public string RouteId { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string ClusterId { get; set; } = string.Empty;

    public string? AuthorizationPolicy { get; set; }

    public int Order { get; set; }

    public ProxyRouteMatchDto Match { get; set; } = new();

    public bool IsEnabled { get; set; }
}
