using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.AI;

/// <summary>
/// LLM 调用用量记录：由 services/ai-gateway 经 POST /api/ai-gateway/usage-records 上报，
/// 供 admin-web「调用记录 / 用量分析」与后续限额/告警使用。
/// </summary>
public class AiUsageRecord : FullAuditedEntity<Guid>
{
    public string Business { get; private set; } = default!;
    public string UsedConfig { get; private set; } = default!;
    public string UsedModel { get; private set; } = default!;
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public int? TotalTokens { get; private set; }
    public string? FinishReason { get; private set; }
    public int Attempts { get; private set; }
    public double? LatencySeconds { get; private set; }
    public string? CircuitBreakerState { get; private set; }
    public bool Success { get; private set; }
    public string? ErrorType { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? TextPreview { get; private set; }

    protected AiUsageRecord()
    {
    }

    public AiUsageRecord(
        Guid id,
        string business,
        string usedConfig,
        string usedModel,
        int? inputTokens,
        int? outputTokens,
        int? totalTokens,
        string? finishReason,
        int attempts,
        double? latencySeconds,
        string? circuitBreakerState,
        bool success,
        string? errorType,
        string? errorMessage,
        string? textPreview) : base(id)
    {
        Business = Check.NotNullOrWhiteSpace(business, nameof(business), maxLength: 64);
        UsedConfig = Check.NotNullOrWhiteSpace(usedConfig, nameof(usedConfig), maxLength: 128);
        UsedModel = Check.NotNullOrWhiteSpace(usedModel, nameof(usedModel), maxLength: 128);
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        TotalTokens = totalTokens;
        FinishReason = finishReason;
        Attempts = attempts;
        LatencySeconds = latencySeconds;
        CircuitBreakerState = circuitBreakerState;
        Success = success;
        ErrorType = errorType;
        ErrorMessage = errorMessage;
        TextPreview = textPreview;
    }
}
