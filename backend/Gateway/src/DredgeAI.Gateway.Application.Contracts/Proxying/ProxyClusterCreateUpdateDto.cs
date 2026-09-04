using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DredgeAI.Gateway.Proxying;

public class ProxyClusterCreateUpdateDto
{
    [Required]
    [StringLength(128)]
    public string ClusterId { get; set; } = default!;

    [Required]
    public Dictionary<string, string> Destinations { get; set; } = new();
}
