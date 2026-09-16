using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DredgeAI.Gateway.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Yarp.ReverseProxy.Configuration;

namespace DredgeAI.Gateway.Proxying;

/// <summary>
/// 首次启动种子：路由表为空时，从配置（appsettings ReverseProxy 节，含环境变量覆盖后的生效值）导入集群与路由；
/// 限流策略表为空时，从 RateLimiting 节插入一条全局固定窗口默认策略。此后 DB 为唯一配置源。
/// </summary>
public class GatewayDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<ProxyRoute, Guid> _routeRepository;
    private readonly IRepository<ProxyCluster, Guid> _clusterRepository;
    private readonly IRepository<RateLimitPolicy, Guid> _policyRepository;
    private readonly IConfiguration _configuration;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<GatewayDataSeedContributor> _logger;

    public GatewayDataSeedContributor(
        IRepository<ProxyRoute, Guid> routeRepository,
        IRepository<ProxyCluster, Guid> clusterRepository,
        IRepository<RateLimitPolicy, Guid> policyRepository,
        IConfiguration configuration,
        IGuidGenerator guidGenerator,
        ILogger<GatewayDataSeedContributor> logger)
    {
        _routeRepository = routeRepository;
        _clusterRepository = clusterRepository;
        _policyRepository = policyRepository;
        _configuration = configuration;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        await SeedRoutesAndClustersAsync();
        await SeedRateLimitPoliciesAsync();
    }

    private async Task SeedRoutesAndClustersAsync()
    {
        if (await _routeRepository.GetCountAsync() > 0)
        {
            return;
        }

        // appsettings 的 Routes/Clusters 以字典键为 ID，对象内部无 ID 字段，需用 with 回填。
        var clusters = _configuration.GetSection("ReverseProxy:Clusters").Get<Dictionary<string, ClusterConfig>>();
        var routes = _configuration.GetSection("ReverseProxy:Routes").Get<Dictionary<string, RouteConfig>>();

        if (clusters is not null)
        {
            foreach (var (clusterId, section) in clusters)
            {
                var config = section with { ClusterId = clusterId };
                if (config.Destinations is null || config.Destinations.Count == 0)
                {
                    _logger.LogWarning("种子跳过无目的地的集群 {ClusterId}", clusterId);
                    continue;
                }
                await _clusterRepository.InsertAsync(
                    new ProxyCluster(_guidGenerator.Create(), config));
            }
        }

        if (routes is not null)
        {
            foreach (var (routeId, section) in routes)
            {
                var config = section with { RouteId = routeId };
                if (string.IsNullOrWhiteSpace(config.ClusterId))
                {
                    _logger.LogWarning("种子跳过缺少 ClusterId 的路由 {RouteId}", routeId);
                    continue;
                }
                await _routeRepository.InsertAsync(
                    new ProxyRoute(_guidGenerator.Create(), config));
            }
        }
    }

    /// <summary>限流策略种子：表为空时从 RateLimiting 配置节插入一条全局固定窗口默认策略。</summary>
    private async Task SeedRateLimitPoliciesAsync()
    {
        if (await _policyRepository.GetCountAsync() > 0)
        {
            return;
        }

        var section = _configuration.GetSection("RateLimiting");
        var permitLimit = section.GetValue("PermitLimit", 100);
        var windowSeconds = section.GetValue("WindowSeconds", 10);
        var queueLimit = section.GetValue("QueueLimit", 0);

        await _policyRepository.InsertAsync(new RateLimitPolicy(
            _guidGenerator.Create(),
            "global-default",
            RateLimitScope.Global,
            routeId: null,
            RateLimitAlgorithm.FixedWindow,
            permitLimit: permitLimit,
            windowSeconds: windowSeconds,
            queueLimit: queueLimit,
            isEnabled: true));
    }
}
