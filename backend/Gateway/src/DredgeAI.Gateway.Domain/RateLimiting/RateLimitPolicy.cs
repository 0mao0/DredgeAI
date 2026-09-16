using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.Gateway.RateLimiting;

/// <summary>限流策略（入库管理；全局/路由级，固定窗口/滑动窗口/令牌桶；运行时由 RateLimiterManager 热生效，路由级优先全局）。</summary>
public class RateLimitPolicy : FullAuditedAggregateRoot<Guid>
{
    /// <summary>显示名，全局唯一。</summary>
    public string Name { get; private set; } = default!;

    /// <summary>作用域：全局 / 路由级。</summary>
    public RateLimitScope Scope { get; private set; }

    /// <summary>目标路由 ID（Scope=Route 时必填；仅冗余 ID 字符串，不加导航/FK）。</summary>
    public string? RouteId { get; private set; }

    /// <summary>限流算法。</summary>
    public RateLimitAlgorithm Algorithm { get; private set; }

    /// <summary>窗口内允许的请求数（Fixed/Sliding 必填 &gt;0；TokenBucket 不用）。</summary>
    public int? PermitLimit { get; private set; }

    /// <summary>窗口时长秒数（Fixed/Sliding 必填 &gt;0）。</summary>
    public int? WindowSeconds { get; private set; }

    /// <summary>滑动窗口分段数（Sliding 必填 &gt;0）。</summary>
    public int? SegmentsPerWindow { get; private set; }

    /// <summary>令牌桶容量（TokenBucket 必填 &gt;0）。</summary>
    public int? TokenLimit { get; private set; }

    /// <summary>每周期补充令牌数（TokenBucket 必填 &gt;0）。</summary>
    public int? TokensPerPeriod { get; private set; }

    /// <summary>令牌补充周期秒数（TokenBucket 必填 &gt;0）。</summary>
    public int? ReplenishmentPeriodSeconds { get; private set; }

    /// <summary>排队上限（&gt;=0，默认 0 不排队）。</summary>
    public int QueueLimit { get; private set; }

    /// <summary>是否启用；禁用的策略不参与限流解析。</summary>
    public bool IsEnabled { get; private set; }

    protected RateLimitPolicy()
    {
    }

    public RateLimitPolicy(
        Guid id,
        string name,
        RateLimitScope scope,
        string? routeId,
        RateLimitAlgorithm algorithm,
        int? permitLimit = null,
        int? windowSeconds = null,
        int? segmentsPerWindow = null,
        int? tokenLimit = null,
        int? tokensPerPeriod = null,
        int? replenishmentPeriodSeconds = null,
        int queueLimit = 0,
        bool isEnabled = true) : base(id)
    {
        SetName(name);
        Scope = scope;
        SetRouteId(routeId);
        Algorithm = algorithm;
        PermitLimit = permitLimit;
        WindowSeconds = windowSeconds;
        SegmentsPerWindow = segmentsPerWindow;
        TokenLimit = tokenLimit;
        TokensPerPeriod = tokensPerPeriod;
        ReplenishmentPeriodSeconds = replenishmentPeriodSeconds;
        QueueLimit = queueLimit;
        IsEnabled = isEnabled;
    }

    public void Update(
        string name,
        RateLimitScope scope,
        string? routeId,
        RateLimitAlgorithm algorithm,
        int? permitLimit = null,
        int? windowSeconds = null,
        int? segmentsPerWindow = null,
        int? tokenLimit = null,
        int? tokensPerPeriod = null,
        int? replenishmentPeriodSeconds = null,
        int queueLimit = 0)
    {
        SetName(name);
        Scope = scope;
        SetRouteId(routeId);
        Algorithm = algorithm;
        PermitLimit = permitLimit;
        WindowSeconds = windowSeconds;
        SegmentsPerWindow = segmentsPerWindow;
        TokenLimit = tokenLimit;
        TokensPerPeriod = tokensPerPeriod;
        ReplenishmentPeriodSeconds = replenishmentPeriodSeconds;
        QueueLimit = queueLimit;
    }

    public void Enable()
    {
        IsEnabled = true;
    }

    public void Disable()
    {
        IsEnabled = false;
    }

    private void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), maxLength: RateLimitPolicyConsts.MaxNameLength);
    }

    private void SetRouteId(string? routeId)
    {
        RouteId = string.IsNullOrWhiteSpace(routeId)
            ? null
            : Check.NotNullOrWhiteSpace(routeId, nameof(routeId), maxLength: RateLimitPolicyConsts.MaxRouteIdLength);
    }
}
