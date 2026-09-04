using System;
using System.Threading.Tasks;
using DredgeAI.Gateway.Proxying;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace DredgeAI.Gateway.EntityFrameworkCore;

public class GatewayDbContextTests : GatewayEntityFrameworkCoreTestBase
{
    [Fact]
    public async Task Should_Query_Seeded_Route_By_RouteId()
    {
        var routeRepo = ServiceProvider.GetRequiredService<IRepository<ProxyRoute, Guid>>();

        var routeA = await routeRepo.FindAsync(x => x.RouteId == "route-a");

        routeA.ShouldNotBeNull();
        routeA.ClusterId.ShouldBe("cluster-a");
        routeA.MatchPath.ShouldBe("/api/a/{**catch-all}");
    }

    [Fact]
    public async Task Should_Query_Seeded_Cluster_By_ClusterId()
    {
        var clusterRepo = ServiceProvider.GetRequiredService<IRepository<ProxyCluster, Guid>>();

        var clusterB = await clusterRepo.FindAsync(x => x.ClusterId == "cluster-b");

        clusterB.ShouldNotBeNull();
        clusterB.DestinationsJson.ShouldContain("http://b:8080/");
    }
}
