using System.Collections.Generic;

namespace DredgeAI.Gateway.Proxying;

public class ProxyRouteMatchDto
{
    public string? Path { get; set; }

    public IReadOnlyList<string>? Hosts { get; set; }
}
