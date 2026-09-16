using System.ComponentModel.DataAnnotations;

namespace DredgeAI.Gateway.RateLimiting;

/// <summary>限流策略创建/更新输入（跨字段算法参数校验在领域层 RateLimitPolicyManager）。</summary>
public class RateLimitPolicyCreateUpdateDto
{
    [Required]
    [StringLength(RateLimitPolicyConsts.MaxNameLength)]
    public string Name { get; set; } = default!;

    [Required]
    public RateLimitScope Scope { get; set; }

    [StringLength(RateLimitPolicyConsts.MaxRouteIdLength)]
    public string? RouteId { get; set; }

    [Required]
    public RateLimitAlgorithm Algorithm { get; set; }

    [Range(1, int.MaxValue)]
    public int? PermitLimit { get; set; }

    [Range(1, int.MaxValue)]
    public int? WindowSeconds { get; set; }

    [Range(1, int.MaxValue)]
    public int? SegmentsPerWindow { get; set; }

    [Range(1, int.MaxValue)]
    public int? TokenLimit { get; set; }

    [Range(1, int.MaxValue)]
    public int? TokensPerPeriod { get; set; }

    [Range(1, int.MaxValue)]
    public int? ReplenishmentPeriodSeconds { get; set; }

    [Range(0, int.MaxValue)]
    public int QueueLimit { get; set; }

    public bool IsEnabled { get; set; } = true;
}
