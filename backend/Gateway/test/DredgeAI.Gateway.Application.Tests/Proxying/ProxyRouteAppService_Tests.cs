using System;
using System.Collections.Generic;
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
    public async Task Create_Should_Roundtrip_With_MatchHosts()
    {
        var created = await _appService.CreateAsync(new ProxyRouteCreateUpdateDto
        {
            RouteId = "route-crud",
            ClusterId = "cluster-a",
            Order = 3,
            MatchPath = "/api/crud/{**catch-all}",
            MatchHosts = new List<string> { "example.com" },
            AuthorizationPolicy = "default"
        });

        created.Id.ShouldNotBe(Guid.Empty);
        created.MatchHosts.ShouldBe(new List<string> { "example.com" });

        var list = await _appService.GetListAsync(new GetProxyRoutesInput { Keyword = "route-crud", MaxResultCount = 10 });
        list.TotalCount.ShouldBe(1);
        list.Items[0].MatchPath.ShouldBe("/api/crud/{**catch-all}");
        list.Items[0].MatchHosts.ShouldBe(new List<string> { "example.com" });
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_RouteId()
    {
        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.CreateAsync(new ProxyRouteCreateUpdateDto
        {
            RouteId = "route-a",
            ClusterId = "cluster-a",
            MatchPath = "/api/dup/{**catch-all}",
            AuthorizationPolicy = "default"
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
            MatchPath = "/api/orphan/{**catch-all}",
            AuthorizationPolicy = "default"
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
            MatchPath = "/api/b2/{**catch-all}",
            AuthorizationPolicy = routeB.AuthorizationPolicy,
            IsEnabled = true
        });

        var snapshot = _configProvider.GetConfig();
        var updated = snapshot.Routes.Single(r => r.RouteId == "route-b");
        updated.Match.Path.ShouldBe("/api/b2/{**catch-all}");
    }
}
