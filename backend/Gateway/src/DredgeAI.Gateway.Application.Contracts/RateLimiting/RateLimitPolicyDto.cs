using System;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.Gateway.RateLimiting;

public class RateLimitPolicyDto : AuditedEntityDto<Guid>
{
    public string Name { get; set; } = default!;

    public RateLimitScope Scope { get; set; }

    public string? RouteId { get; set; }

    public RateLimitAlgorithm Algorithm { get; set; }

    public int? PermitLimit { get; set; }

    public int? WindowSeconds { get; set; }

    public int? SegmentsPerWindow { get; set; }

    public int? TokenLimit { get; set; }

    public int? TokensPerPeriod { get; set; }

    public int? ReplenishmentPeriodSeconds { get; set; }

    public int QueueLimit { get; set; }

    public bool IsEnabled { get; set; }
}
