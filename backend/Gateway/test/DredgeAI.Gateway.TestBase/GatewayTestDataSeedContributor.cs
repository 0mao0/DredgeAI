using System.Collections.Generic;
using System.Threading.Tasks;
using DredgeAI.Gateway.Proxying;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Yarp.ReverseProxy.Configuration;

namespace DredgeAI.Gateway;

public class GatewayTestDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<ProxyRoute, System.Guid> _routeRepository;
    private readonly IRepository<ProxyCluster, System.Guid> _clusterRepository;
    private readonly IGuidGenerator _guidGenerator;

    public GatewayTestDataSeedContributor(
        IRepository<ProxyRoute, System.Guid> routeRepository,
        IRepository<ProxyCluster, System.Guid> clusterRepository,
        IGuidGenerator guidGenerator)
    {
        _routeRepository = routeRepository;
        _clusterRepository = clusterRepository;
        _guidGenerator = guidGenerator;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (await _clusterRepository.GetCountAsync() > 0)
        {
            return;
        }

        await _clusterRepository.InsertAsync(new ProxyCluster(
            _guidGenerator.Create(),
            new ClusterConfig
            {
                ClusterId = "cluster-a",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["destination1"] = new() { Address = "http://a:8080/" }
                }
            }));
        await _clusterRepository.InsertAsync(new ProxyCluster(
            _guidGenerator.Create(),
            new ClusterConfig
            {
                ClusterId = "cluster-b",
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["destination1"] = new() { Address = "http://b:8080/" }
                }
            }));

        await _routeRepository.InsertAsync(new ProxyRoute(
            _guidGenerator.Create(),
            new RouteConfig
            {
                RouteId = "route-a",
                ClusterId = "cluster-a",
                Match = new RouteMatch { Path = "/api/a/{**catch-all}" }
            }));
        await _routeRepository.InsertAsync(new ProxyRoute(
            _guidGenerator.Create(),
            new RouteConfig
            {
                RouteId = "route-b",
                ClusterId = "cluster-b",
                Match = new RouteMatch { Path = "/api/b/{**catch-all}" }
            }));
    }
}
