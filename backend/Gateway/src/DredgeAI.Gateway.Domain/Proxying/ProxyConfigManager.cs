using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace DredgeAI.Gateway.Proxying;

/// <summary>代理配置领域服务：路由/集群写入前的业务校验。</summary>
public class ProxyConfigManager : DomainService
{
    private readonly IRepository<ProxyRoute, Guid> _routeRepository;
    private readonly IRepository<ProxyCluster, Guid> _clusterRepository;

    public ProxyConfigManager(
        IRepository<ProxyRoute, Guid> routeRepository,
        IRepository<ProxyCluster, Guid> clusterRepository)
    {
        _routeRepository = routeRepository;
        _clusterRepository = clusterRepository;
    }

    public async Task ValidateNewRouteAsync(ProxyRoute route)
    {
        if (await _routeRepository.AnyAsync(x => x.RouteId == route.RouteId))
        {
            throw new BusinessException(GatewayErrorCodes.DuplicateRouteId);
        }

        await ValidateRouteClusterExistsAsync(route);
    }

    public async Task ValidateUpdateRouteAsync(ProxyRoute route)
    {
        if (await _routeRepository.AnyAsync(x => x.RouteId == route.RouteId && x.Id != route.Id))
        {
            throw new BusinessException(GatewayErrorCodes.DuplicateRouteId);
        }

        await ValidateRouteClusterExistsAsync(route);
    }

    public async Task ValidateClusterAsync(ProxyCluster cluster, Guid? excludeId = null)
    {
        if (await _clusterRepository.AnyAsync(x => x.ClusterId == cluster.ClusterId && x.Id != excludeId))
        {
            throw new BusinessException(GatewayErrorCodes.DuplicateClusterId);
        }

        var destinations = JsonSerializer.Deserialize<Dictionary<string, string>>(cluster.DestinationsJson);
        if (destinations is null || destinations.Count == 0)
        {
            throw new BusinessException(GatewayErrorCodes.InvalidDestinationAddress);
        }

        foreach (var address in destinations.Values)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new BusinessException(GatewayErrorCodes.InvalidDestinationAddress);
            }
        }
    }

    public async Task ValidateClusterDeleteAsync(string clusterId)
    {
        if (await _routeRepository.AnyAsync(x => x.ClusterId == clusterId))
        {
            throw new BusinessException(GatewayErrorCodes.ClusterInUse);
        }
    }

    private async Task ValidateRouteClusterExistsAsync(ProxyRoute route)
    {
        if (!await _clusterRepository.AnyAsync(x => x.ClusterId == route.ClusterId))
        {
            throw new BusinessException(GatewayErrorCodes.ClusterNotFound);
        }
    }
}
