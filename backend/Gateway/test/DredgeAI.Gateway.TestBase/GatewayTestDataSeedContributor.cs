using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using DredgeAI.Gateway.Proxying;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

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
            _guidGenerator.Create(), "cluster-a",
            JsonSerializer.Serialize(new Dictionary<string, string> { ["destination1"] = "http://a:8080/" })));
        await _clusterRepository.InsertAsync(new ProxyCluster(
            _guidGenerator.Create(), "cluster-b",
            JsonSerializer.Serialize(new Dictionary<string, string> { ["destination1"] = "http://b:8080/" })));

        await _routeRepository.InsertAsync(new ProxyRoute(
            _guidGenerator.Create(), "route-a", "cluster-a", 0, "/api/a/{**catch-all}", null, null, "default"));
        await _routeRepository.InsertAsync(new ProxyRoute(
            _guidGenerator.Create(), "route-b", "cluster-b", 0, "/api/b/{**catch-all}", null, null, "default"));
    }
}
