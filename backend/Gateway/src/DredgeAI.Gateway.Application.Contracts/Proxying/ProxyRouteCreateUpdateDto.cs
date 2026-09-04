using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DredgeAI.Gateway.Proxying;

public class ProxyRouteCreateUpdateDto
{
    [Required]
    [StringLength(128)]
    public string RouteId { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string ClusterId { get; set; } = default!;

    public int Order { get; set; }

    [Required]
    [StringLength(256)]
    public string MatchPath { get; set; } = default!;

    public List<string>? MatchHosts { get; set; }

    public List<string>? MatchMethods { get; set; }

    [Required]
    [StringLength(64)]
    public string AuthorizationPolicy { get; set; } = "default";

    public bool IsEnabled { get; set; } = true;
}
