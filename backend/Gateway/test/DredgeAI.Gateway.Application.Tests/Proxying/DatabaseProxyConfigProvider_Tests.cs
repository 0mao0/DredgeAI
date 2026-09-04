using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace DredgeAI.Gateway.Proxying;

public class DatabaseProxyConfigProvider_Tests : GatewayApplicationTestBase<GatewayApplicationTestModule>
{
    private readonly DatabaseProxyConfigProvider _configProvider;

    public DatabaseProxyConfigProvider_Tests()
    {
        _configProvider = GetRequiredService<DatabaseProxyConfigProvider>();
    }

    [Fact]
    public void Initial_Snapshot_Should_Contain_Seeded_Routes_And_Clusters()
    {
        var config = _configProvider.GetConfig();

        config.Routes.Select(x => x.RouteId).ShouldBeSubsetOf(new[] { "route-a", "route-b" });
        config.Routes.Count.ShouldBe(2);
        config.Clusters.Select(x => x.ClusterId).ShouldBeSubsetOf(new[] { "cluster-a", "cluster-b" });
        config.Clusters.Count.ShouldBe(2);

        var routeA = config.Routes.Single(x => x.RouteId == "route-a");
        routeA.Match!.Path.ShouldBe("/api/a/{**catch-all}");
        routeA.ClusterId.ShouldBe("cluster-a");

        var clusterA = config.Clusters.Single(x => x.ClusterId == "cluster-a");
        clusterA.Destinations["destination1"]!.Address.ShouldBe("http://a:8080/");
    }

    [Fact]
    public void Reload_Should_Signal_Old_Snapshot_Token()
    {
        var old = _configProvider.GetConfig();

        _configProvider.Reload();

        old.ChangeToken.HasChanged.ShouldBeTrue();
    }

    [Fact]
    public async Task Disabled_Route_Should_Be_Excluded_After_Reload()
    {
        using var scope = ServiceProvider.CreateScope();
        var routeRepository = scope.ServiceProvider.GetRequiredService<IRepository<ProxyRoute, Guid>>();

        var routeA = await routeRepository.GetAsync(x => x.RouteId == "route-a");
        routeA.Disable();
        await routeRepository.UpdateAsync(routeA, autoSave: true);

        _configProvider.Reload();

        var config = _configProvider.GetConfig();

        config.Routes.ShouldNotContain(x => x.RouteId == "route-a");
        config.Routes.ShouldContain(x => x.RouteId == "route-b");

        // 恢复，避免影响同类中其他用例（共享单例 provider 与内存库）
        routeA.Enable();
        await routeRepository.UpdateAsync(routeA, autoSave: true);
        _configProvider.Reload();
    }
}
