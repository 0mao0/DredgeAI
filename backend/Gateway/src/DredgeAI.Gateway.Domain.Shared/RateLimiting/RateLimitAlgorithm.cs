namespace DredgeAI.Gateway.RateLimiting;

/// <summary>限流算法。</summary>
public enum RateLimitAlgorithm
{
    /// <summary>固定窗口。</summary>
    FixedWindow = 0,

    /// <summary>滑动窗口。</summary>
    SlidingWindow = 1,

    /// <summary>令牌桶。</summary>
    TokenBucket = 2
}
