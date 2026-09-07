using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DredgeAI.Gateway.Proxying;

public class ProxyClusterCreateUpdateDto
{
    [Required]
    [StringLength(128)]
    public string ClusterId { get; set; } = string.Empty;

    [StringLength(256)]
    public string? Description { get; set; }

    [Required]
    public Dictionary<string, ClusterDestinationDto> Destinations { get; set; } = new();

    public bool IsEnabled { get; set; } = true;
}
