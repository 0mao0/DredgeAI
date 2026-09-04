using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.Gateway.Proxying;

public class ProxyClusterDto : AuditedEntityDto<Guid>
{
    public string ClusterId { get; set; } = default!;

    public Dictionary<string, string> Destinations { get; set; } = new();
}
