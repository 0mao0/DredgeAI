using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Yarp.ReverseProxy.Model;

namespace DredgeAI.Gateway.RateLimiting;

/// <summary>
/// 动态限流管理器：从 DB 加载 RateLimitPolicy 快照，按策略构建并缓存 PartitionedRateLimiter（按客户端 IP 分区）。
/// 写操作落库后由 AppService 调 ReloadAsync() 触发热重载；DB 不可用时返回空快照不抛异常。
/// 解析优先级：路由级策略 &gt; 全局策略；无匹配启用策略则不限流。
/// </summary>
public sealed class RateLimiterManager
{
    private const string NoPolicyKey = "dredge:no-policy";

    private static readonly Lazy<PartitionedRateLimiter<string>> NoLimiterByIp = new(() =>
        PartitionedRateLimiter.Create<string, string>(_ => RateLimitPartition.GetNoLimiter("allow-all")));

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RateLimiterManager> _logger;
    private readonly ConcurrentDictionary<Guid, PartitionedRateLimiter<string>> _limiters = new();
    private volatile IReadOnlyList<RateLimitPolicy> _snapshot;

    public RateLimiterManager(IServiceScopeFactory scopeFactory, ILogger<RateLimiterManager> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _snapshot = AsyncHelper.RunSync(LoadAsync);
    }

    public async Task ReloadAsync()
    {
        _snapshot = await LoadAsync();

        // 策略快照变了即整体重建限流器缓存，避免旧参数残留
        foreach (var key in _limiters.Keys.ToList())
        {
            if (_limiters.TryRemove(key, out var limiter))
            {
                await limiter.DisposeAsync();
            }
        }
    }

    /// <summary>解析给定路由的生效策略：路由级（启用）优先，其次全局（启用），都无返回 null（不限流）。</summary>
    public RateLimitPolicy? ResolveEffectivePolicy(string? routeId)
    {
        var snapshot = _snapshot;

        if (!routeId.IsNullOrWhiteSpace())
        {
            var routePolicy = snapshot.FirstOrDefault(x =>
                x.Scope == RateLimitScope.Route && x.RouteId == routeId && x.IsEnabled);
            if (routePolicy is not null)
            {
                return routePolicy;
            }
        }

        return snapshot.FirstOrDefault(x => x.Scope == RateLimitScope.Global && x.IsEnabled);
    }

    /// <summary>
    /// 供 AddRateLimiter 策略工厂调用：逐请求解析生效策略，返回按 (策略ID, 客户端IP) 缓存的分区。
    /// 分区工厂返回的 IpRateLimiter 每次 Acquire 都从缓存取当前限流器，Reload 后自动生效新参数。
    /// </summary>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();
        var routeId = endpoint?.Metadata.GetMetadata<RouteModel>()?.Config.RouteId ?? endpoint?.DisplayName;

        var policy = ResolveEffectivePolicy(routeId);
        if (policy is null)
        {
            return RateLimitPartition.GetNoLimiter(NoPolicyKey);
        }

        var ip = GetClientIp(httpContext);
        return RateLimitPartition.Get($"{policy.Id:N}:{ip}", _ => new IpRateLimiter(this, policy.Id, ip));
    }

    /// <summary>取策略对应的 IP 分区限流器；策略已从快照移除时返回不限流的静态实例。</summary>
    internal PartitionedRateLimiter<string> GetOrCreateLimiter(Guid policyId)
    {
        return _limiters.GetOrAdd(policyId, id =>
        {
            var policy = _snapshot.FirstOrDefault(x => x.Id == id);
            return policy is null ? NoLimiterByIp.Value : Build(policy);
        });
    }

    private async Task<IReadOnlyList<RateLimitPolicy>> LoadAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IRepository<RateLimitPolicy, Guid>>();
            return await repository.GetListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load rate limit policies from database; falling back to empty snapshot (no rate limiting).");
            return Array.Empty<RateLimitPolicy>();
        }
    }

    private static PartitionedRateLimiter<string> Build(RateLimitPolicy policy)
    {
        return policy.Algorithm switch
        {
            RateLimitAlgorithm.FixedWindow => PartitionedRateLimiter.Create<string, string>(
                ip => RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = policy.PermitLimit!.Value,
                    Window = TimeSpan.FromSeconds(policy.WindowSeconds!.Value),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = policy.QueueLimit
                })),
            RateLimitAlgorithm.SlidingWindow => PartitionedRateLimiter.Create<string, string>(
                ip => RateLimitPartition.GetSlidingWindowLimiter(ip, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = policy.PermitLimit!.Value,
                    Window = TimeSpan.FromSeconds(policy.WindowSeconds!.Value),
                    SegmentsPerWindow = policy.SegmentsPerWindow!.Value,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = policy.QueueLimit
                })),
            RateLimitAlgorithm.TokenBucket => PartitionedRateLimiter.Create<string, string>(
                ip => RateLimitPartition.GetTokenBucketLimiter(ip, _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = policy.TokenLimit!.Value,
                    TokensPerPeriod = policy.TokensPerPeriod!.Value,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(policy.ReplenishmentPeriodSeconds!.Value),
                    AutoReplenishment = true,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = policy.QueueLimit
                })),
            _ => NoLimiterByIp.Value
        };
    }

    private static string GetClientIp(HttpContext ctx)
    {
        // UseForwardedHeaders 已在管线最前还原真实客户端 IP
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>
    /// 单个 (策略, IP) 分区的适配限流器：每次 Acquire 从 RateLimiterManager 取当前限流器委托，
    /// 不直接持有内部限流器引用，ReloadAsync 换缓存后无需中间件层驱逐即生效新参数。
    /// </summary>
    private sealed class IpRateLimiter : RateLimiter
    {
        private readonly RateLimiterManager _manager;
        private readonly Guid _policyId;
        private readonly string _ip;

        public IpRateLimiter(RateLimiterManager manager, Guid policyId, string ip)
        {
            _manager = manager;
            _policyId = policyId;
            _ip = ip;
        }

        public override TimeSpan? IdleDuration => null;

        public override RateLimiterStatistics? GetStatistics()
        {
            return _manager.GetOrCreateLimiter(_policyId).GetStatistics(_ip);
        }

        protected override RateLimitLease AttemptAcquireCore(int permitCount)
        {
            return _manager.GetOrCreateLimiter(_policyId).AttemptAcquire(_ip, permitCount);
        }

        protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken)
        {
            return _manager.GetOrCreateLimiter(_policyId).AcquireAsync(_ip, permitCount, cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            // 不释放共享的缓存限流器
        }
    }
}
