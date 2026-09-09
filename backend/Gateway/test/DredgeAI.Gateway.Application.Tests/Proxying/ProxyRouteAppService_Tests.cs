using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace DredgeAI.Gateway.Proxying;

public class ProxyRouteAppService_Tests : GatewayApplicationTestBase<GatewayApplicationTestModule>
{
    private readonly IProxyRouteAppService _appService;
    private readonly DatabaseProxyConfigProvider _configProvider;

    public ProxyRouteAppService_Tests()
    {
        _appService = GetRequiredService<IProxyRouteAppService>();
        _configProvider = GetRequiredService<DatabaseProxyConfigProvider>();
    }

    [Fact]
    public async Task Create_Should_Roundtrip_Simplified_RouteConfig()
    {
        var created = await _appService.CreateAsync(new ProxyRouteCreateUpdateDto
        {
            RouteId = "route-crud",
            ClusterId = "cluster-a",
            Order = 3,
            AuthorizationPolicy = "anonymous",
            Description = "ops",
            Match = new ProxyRouteMatchDto { Path = "/api/crud/{**catch-all}", Hosts = new[] { "example.com" } }
        });

        created.Id.ShouldNotBe(Guid.Empty);

        // GetAsync 扁平字段往返
        var fetched = await _appService.GetAsync(created.Id);
        fetched.Match.Path.ShouldBe("/api/crud/{**catch-all}");
        fetched.Match.Hosts.ShouldBe(new[] { "example.com" });
        fetched.AuthorizationPolicy.ShouldBe("anonymous");
        fetched.Description.ShouldBe("ops");
        fetched.Order.ShouldBe(3);

        var list = await _appService.GetListAsync(new GetProxyRoutesInput { Keyword = "route-crud", MaxResultCount = 10 });
        list.TotalCount.ShouldBe(1);
        list.Items[0].Match.Path.ShouldBe("/api/crud/{**catch-all}");

        // 快照内同字段生效
        var snapshot = _configProvider.GetConfig();
        var snapRoute = snapshot.Routes.Single(r => r.RouteId == "route-crud");
        snapRoute.Match.Hosts.ShouldBe(new[] { "example.com" });
        snapRoute.AuthorizationPolicy.ShouldBe("anonymous");
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_RouteId()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.CreateAsync(new ProxyRouteCreateUpdateDto
        {
            RouteId = "route-a",
            ClusterId = "cluster-a",
            Match = new ProxyRouteMatchDto { Path = "/api/dup/{**catch-all}" }
        }));

        ex.Code.ShouldBe(GatewayErrorCodes.DuplicateRouteId);
    }

    [Fact]
    public async Task Create_Should_Reject_Missing_Cluster()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.CreateAsync(new ProxyRouteCreateUpdateDto
        {
            RouteId = "route-orphan",
            ClusterId = "cluster-missing",
            Match = new ProxyRouteMatchDto { Path = "/api/orphan/{**catch-all}" }
        }));

        ex.Code.ShouldBe(GatewayErrorCodes.ClusterNotFound);
    }

    [Fact]
    public async Task Update_Should_Refresh_Yarp_Snapshot()
    {
        var list = await _appService.GetListAsync(new GetProxyRoutesInput { Keyword = "route-b", MaxResultCount = 10 });
        var routeB = list.Items.Single();

        await _appService.UpdateAsync(routeB.Id, new ProxyRouteCreateUpdateDto
        {
            RouteId = routeB.RouteId,
            ClusterId = routeB.ClusterId,
            Order = routeB.Order,
            AuthorizationPolicy = routeB.AuthorizationPolicy,
            Description = routeB.Description,
            Match = new ProxyRouteMatchDto { Path = "/api/b2/{**catch-all}" },
            IsEnabled = true
        });

        var snapshot = _configProvider.GetConfig();
        var updated = snapshot.Routes.Single(r => r.RouteId == "route-b");
        updated.Match.Path.ShouldBe("/api/b2/{**catch-all}");
    }
}
