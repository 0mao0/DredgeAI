using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.BidCompare.AI;

public class AiUsageRecordDto : FullAuditedEntityDto<Guid>
{
    public string Business { get; set; } = default!;
    public string UsedConfig { get; set; } = default!;
    public string UsedModel { get; set; } = default!;
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string? FinishReason { get; set; }
    public int Attempts { get; set; }
    public double? LatencySeconds { get; set; }
    public string? CircuitBreakerState { get; set; }
    public bool Success { get; set; }
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TextPreview { get; set; }
}

public class CreateAiUsageRecordDto
{
    public string Business { get; set; } = "general";
    public string UsedConfig { get; set; } = "";
    public string UsedModel { get; set; } = "";
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string? FinishReason { get; set; }
    public int Attempts { get; set; } = 1;
    public double? LatencySeconds { get; set; }
    public string? CircuitBreakerState { get; set; }
    public bool Success { get; set; } = true;
    public string? ErrorType { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TextPreview { get; set; }
}

public class GetAiUsageRecordsInput : PagedAndSortedResultRequestDto
{
    public string? Business { get; set; }
    public string? Model { get; set; }
    public bool? Success { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}

public class AiUsageStatsDto
{
    public long TotalCalls { get; set; }
    public long TotalTokens { get; set; }
}

public class UsageSeriesItemDto
{
    public string Name { get; set; } = default!;
    public List<int> Data { get; set; } = new();
}

public class UsageTimeSeriesDto
{
    public List<string> Categories { get; set; } = new();
    public List<UsageSeriesItemDto> ByModel { get; set; } = new();
    public List<UsageSeriesItemDto> ByKey { get; set; } = new();
    public List<UsageSeriesItemDto> ByName { get; set; } = new();
}
