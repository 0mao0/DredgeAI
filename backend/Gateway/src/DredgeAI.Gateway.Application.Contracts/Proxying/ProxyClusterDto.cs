using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.Gateway.Proxying;

public class ProxyClusterDto : AuditedEntityDto<Guid>
{
    public string ClusterId { get; set; } = string.Empty;

    public string? Description { get; set; }

    public Dictionary<string, ClusterDestinationDto> Destinations { get; set; } = new();

    public bool IsEnabled { get; set; }
}
