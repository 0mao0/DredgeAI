using Volo.Abp.Application.Dtos;

namespace DredgeAI.Gateway.RateLimiting;

public class GetRateLimitPoliciesInput : PagedAndSortedResultRequestDto
{
    /// <summary>匹配 Name / RouteId。</summary>
    public string? Keyword { get; set; }

    public RateLimitScope? Scope { get; set; }

    public string? RouteId { get; set; }
}
