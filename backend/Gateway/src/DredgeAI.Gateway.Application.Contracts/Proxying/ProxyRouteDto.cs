using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.Gateway.Proxying;

public class ProxyRouteDto : AuditedEntityDto<Guid>
{
    public string RouteId { get; set; } = default!;

    public string ClusterId { get; set; } = default!;

    public int Order { get; set; }

    public string MatchPath { get; set; } = default!;

    public List<string>? MatchHosts { get; set; }

    public List<string>? MatchMethods { get; set; }

    public string AuthorizationPolicy { get; set; } = default!;

    public bool IsEnabled { get; set; }
}
