using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace DredgeAI.Gateway.Proxying;

/// <summary>
/// 首次启动种子：路由表为空时，从配置（appsettings ReverseProxy 节，含环境变量覆盖后的生效值）导入集群与路由。
/// 此后 DB 为唯一配置源。
/// </summary>
public class GatewayDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<ProxyRoute, Guid> _routeRepository;
    private readonly IRepository<ProxyCluster, Guid> _clusterRepository;
    private readonly IConfiguration _configuration;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<GatewayDataSeedContributor> _logger;

    public GatewayDataSeedContributor(
        IRepository<ProxyRoute, Guid> routeRepository,
        IRepository<ProxyCluster, Guid> clusterRepository,
        IConfiguration configuration,
        IGuidGenerator guidGenerator,
        ILogger<GatewayDataSeedContributor> logger)
    {
        _routeRepository = routeRepository;
        _clusterRepository = clusterRepository;
        _configuration = configuration;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (await _routeRepository.GetCountAsync() > 0)
        {
            return;
        }

        var clusters = _configuration.GetSection("ReverseProxy:Clusters").Get<Dictionary<string, ClusterSection>>();
        var routes = _configuration.GetSection("ReverseProxy:Routes").Get<Dictionary<string, RouteSection>>();

        if (clusters is not null)
        {
            foreach (var (clusterId, cluster) in clusters)
            {
                var destinations = new Dictionary<string, string>();
                if (cluster.Destinations is not null)
                {
                    foreach (var (destinationId, destination) in cluster.Destinations)
                    {
                        if (string.IsNullOrWhiteSpace(destination.Address))
                        {
                            _logger.LogWarning("种子跳过集群 {ClusterId} 的空地址目的地 {DestinationId}", clusterId, destinationId);
                            continue;
                        }
                        destinations[destinationId] = destination.Address;
                    }
                }
                await _clusterRepository.InsertAsync(
                    new ProxyCluster(_guidGenerator.Create(), clusterId, JsonSerializer.Serialize(destinations)));
            }
        }

        if (routes is not null)
        {
            foreach (var (routeId, route) in routes)
            {
                if (string.IsNullOrWhiteSpace(route.Match?.Path))
                {
                    _logger.LogWarning("种子跳过缺少 Match.Path 的路由 {RouteId}", routeId);
                    continue;
                }

                await _routeRepository.InsertAsync(
                    new ProxyRoute(
                        _guidGenerator.Create(),
                        routeId,
                        route.ClusterId ?? string.Empty,
                        route.Order,
                        route.Match.Path,
                        route.Match.Hosts is { Length: > 0 } ? JsonSerializer.Serialize(route.Match.Hosts) : null,
                        route.Match.Methods is { Length: > 0 } ? JsonSerializer.Serialize(route.Match.Methods) : null,
                        string.IsNullOrWhiteSpace(route.AuthorizationPolicy) ? "default" : route.AuthorizationPolicy));
            }
        }
    }

    private sealed class RouteSection
    {
        public string? ClusterId { get; set; }
        public string? AuthorizationPolicy { get; set; }
        public int Order { get; set; }
        public MatchSection? Match { get; set; }
    }

    private sealed class MatchSection
    {
        public string? Path { get; set; }
        public string[]? Hosts { get; set; }
        public string[]? Methods { get; set; }
    }

    private sealed class ClusterSection
    {
        public Dictionary<string, DestinationSection>? Destinations { get; set; }
    }

    private sealed class DestinationSection
    {
        public string? Address { get; set; }
    }
}
