using System;
using Shouldly;
using Xunit;

namespace DredgeAI.Gateway.Proxying;

public class ProxyRouteTests
{
    [Fact]
    public void Ctor_Should_Throw_On_Empty_RouteId()
    {
        Should.Throw<ArgumentException>(() =>
            new ProxyRoute(Guid.NewGuid(), " ", "cluster-a", 0, "/api/a/{**catch-all}", null, null, "default"));
    }

    [Fact]
    public void Ctor_Should_Throw_On_Empty_MatchPath()
    {
        Should.Throw<ArgumentException>(() =>
            new ProxyRoute(Guid.NewGuid(), "route-x", "cluster-a", 0, "", null, null, "default"));
    }

    [Fact]
    public void Ctor_Should_Default_Enabled()
    {
        var route = new ProxyRoute(Guid.NewGuid(), "route-x", "cluster-a", 1, "/api/x/{**catch-all}", null, null, "anonymous");
        route.IsEnabled.ShouldBeTrue();
        route.AuthorizationPolicy.ShouldBe("anonymous");
    }

    [Fact]
    public void Update_Should_Replace_Values()
    {
        var route = new ProxyRoute(Guid.NewGuid(), "route-x", "cluster-a", 0, "/api/x/{**catch-all}", null, null, "default");

        route.Update("route-y", "cluster-b", 5, "/api/y/{**catch-all}", "[\"h1\"]", "[\"GET\"]", "anonymous");

        route.RouteId.ShouldBe("route-y");
        route.ClusterId.ShouldBe("cluster-b");
        route.Order.ShouldBe(5);
        route.MatchPath.ShouldBe("/api/y/{**catch-all}");
        route.MatchHostsJson.ShouldBe("[\"h1\"]");
        route.MatchMethodsJson.ShouldBe("[\"GET\"]");
        route.AuthorizationPolicy.ShouldBe("anonymous");
    }

    [Fact]
    public void Enable_Disable_Should_Flip_IsEnabled()
    {
        var route = new ProxyRoute(Guid.NewGuid(), "route-x", "cluster-a", 0, "/api/x/{**catch-all}", null, null, "default");

        route.Disable();
        route.IsEnabled.ShouldBeFalse();

        route.Enable();
        route.IsEnabled.ShouldBeTrue();
    }
}
