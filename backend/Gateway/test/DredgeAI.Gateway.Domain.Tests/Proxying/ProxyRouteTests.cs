using System;
using Shouldly;
using Xunit;
using Yarp.ReverseProxy.Configuration;

namespace DredgeAI.Gateway.Proxying;

public class ProxyRouteTests
{
    [Fact]
    public void Ctor_Should_Throw_On_Empty_RouteId()
    {
        Should.Throw<ArgumentException>(() =>
            new ProxyRoute(Guid.NewGuid(), new RouteConfig
            {
                RouteId = " ",
                ClusterId = "cluster-a",
                Match = new RouteMatch { Path = "/api/a/{**catch-all}" }
            }));
    }

    [Fact]
    public void Ctor_Should_Default_Enabled()
    {
        var route = new ProxyRoute(Guid.NewGuid(), new RouteConfig
        {
            RouteId = "route-x",
            ClusterId = "cluster-a",
            AuthorizationPolicy = "anonymous",
            Match = new RouteMatch { Path = "/api/x/{**catch-all}" }
        });

        route.IsEnabled.ShouldBeTrue();
        route.ToRouteConfig().AuthorizationPolicy.ShouldBe("anonymous");
    }

    [Fact]
    public void Update_Should_Replace_Config()
    {
        var route = new ProxyRoute(Guid.NewGuid(), new RouteConfig
        {
            RouteId = "route-x",
            ClusterId = "cluster-a",
            Match = new RouteMatch { Path = "/api/x/{**catch-all}" }
        });

        route.Update(new RouteConfig
        {
            RouteId = "route-y",
            ClusterId = "cluster-b",
            Order = 5,
            AuthorizationPolicy = "anonymous",
            Match = new RouteMatch
            {
                Path = "/api/y/{**catch-all}",
                Hosts = new[] { "h1" },
                Methods = new[] { "GET" }
            }
        });

        route.RouteId.ShouldBe("route-y");
        route.ClusterId.ShouldBe("cluster-b");
        route.Order.ShouldBe(5);

        var config = route.ToRouteConfig();
        config.Match.Path.ShouldBe("/api/y/{**catch-all}");
        config.Match.Hosts.ShouldBe(new[] { "h1" });
        config.Match.Methods.ShouldBe(new[] { "GET" });
        config.AuthorizationPolicy.ShouldBe("anonymous");
    }

    [Fact]
    public void Enable_Disable_Should_Flip_IsEnabled()
    {
        var route = new ProxyRoute(Guid.NewGuid(), new RouteConfig
        {
            RouteId = "route-x",
            ClusterId = "cluster-a",
            Match = new RouteMatch { Path = "/api/x/{**catch-all}" }
        });

        route.Disable();
        route.IsEnabled.ShouldBeFalse();

        route.Enable();
        route.IsEnabled.ShouldBeTrue();
    }
}
