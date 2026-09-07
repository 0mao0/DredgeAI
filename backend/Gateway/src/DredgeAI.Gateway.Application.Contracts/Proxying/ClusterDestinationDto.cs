using System.ComponentModel.DataAnnotations;

namespace DredgeAI.Gateway.Proxying;

public class ClusterDestinationDto
{
    [Required]
    public string Address { get; set; } = string.Empty;

    public string? Health { get; set; }
}
