namespace DredgeAI.Gateway.RateLimiting;

/// <summary>限流策略作用域。</summary>
public enum RateLimitScope
{
    /// <summary>全局策略：作用于全部代理端点。</summary>
    Global = 0,

    /// <summary>路由级策略：仅作用于指定路由，优先于全局策略。</summary>
    Route = 1
}
