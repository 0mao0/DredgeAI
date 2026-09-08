using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace DredgeAI.Gateway.Proxying;

public class GatewayDataSeedContributor_Tests : GatewayApplicationTestBase<GatewayApplicationTestModule>
{
    [Fact]
    public async Task Seed_Should_Import_ReverseProxy_Section_And_Be_Idempotent()
    {
        using var scope = ServiceProvider.CreateScope();
        var routeRepository = scope.ServiceProvider.GetRequiredService<IRepository<ProxyRoute, Guid>>();
        var clusterRepository = scope.ServiceProvider.GetRequiredService<IRepository<ProxyCluster, Guid>>();

        // 清空两表（含测试种子数据）
        foreach (var route in await routeRepository.GetListAsync())
        {
            await routeRepository.HardDeleteAsync(route, autoSave: true);
        }
        foreach (var cluster in await clusterRepository.GetListAsync())
        {
            await clusterRepository.HardDeleteAsync(cluster, autoSave: true);
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:Clusters:cluster-seed:Destinations:destination1:Address"] = "http://seed:8080/",
                ["ReverseProxy:Routes:route-seed:ClusterId"] = "cluster-seed",
                ["ReverseProxy:Routes:route-seed:Order"] = "7",
                ["ReverseProxy:Routes:route-seed:AuthorizationPolicy"] = "anonymous",
                ["ReverseProxy:Routes:route-seed:Match:Path"] = "/api/seed/{**catch-all}",
                ["ReverseProxy:Routes:route-seed:Match:Hosts:0"] = "seed.example.com"
            })
            .Build();

        var contributor = new GatewayDataSeedContributor(
            routeRepository,
            clusterRepository,
            configuration,
            SimpleGuidGenerator.Instance,
            NullLogger<GatewayDataSeedContributor>.Instance);

        await contributor.SeedAsync(new DataSeedContext());

        (await clusterRepository.GetCountAsync()).ShouldBe(1);
        (await routeRepository.GetCountAsync()).ShouldBe(1);

        var seededRoute = await routeRepository.GetAsync(x => x.RouteId == "route-seed");
        seededRoute.ClusterId.ShouldBe("cluster-seed");
        seededRoute.Order.ShouldBe(7);

        var seededConfig = seededRoute.ToRouteConfig();
        seededConfig.Match.Path.ShouldBe("/api/seed/{**catch-all}");
        seededConfig.Match.Hosts.ShouldBe(new[] { "seed.example.com" });
        seededConfig.AuthorizationPolicy.ShouldBe("anonymous");

        // 幂等：再次执行行数不变
        await contributor.SeedAsync(new DataSeedContext());
        (await clusterRepository.GetCountAsync()).ShouldBe(1);
        (await routeRepository.GetCountAsync()).ShouldBe(1);
    }
}
