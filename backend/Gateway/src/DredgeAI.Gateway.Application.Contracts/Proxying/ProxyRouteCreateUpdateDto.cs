using System.ComponentModel.DataAnnotations;

namespace DredgeAI.Gateway.Proxying;

public class ProxyRouteCreateUpdateDto
{
    [Required]
    [StringLength(128)]
    public string RouteId { get; set; } = string.Empty;

    [StringLength(256)]
    public string? Description { get; set; }

    [Required]
    [StringLength(128)]
    public string ClusterId { get; set; } = string.Empty;

    public string? AuthorizationPolicy { get; set; }

    public int Order { get; set; }

    [Required]
    public ProxyRouteMatchDto Match { get; set; } = new();

    public bool IsEnabled { get; set; } = true;
}
