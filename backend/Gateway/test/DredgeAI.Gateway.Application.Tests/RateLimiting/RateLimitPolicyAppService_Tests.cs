using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace DredgeAI.Gateway.RateLimiting;

public class RateLimitPolicyAppService_Tests : GatewayApplicationTestBase<GatewayApplicationTestModule>
{
    private readonly IRateLimitPolicyAppService _appService;
    private readonly RateLimiterManager _rateLimiterManager;
    private readonly IRepository<RateLimitPolicy, Guid> _repository;

    public RateLimitPolicyAppService_Tests()
    {
        _appService = GetRequiredService<IRateLimitPolicyAppService>();
        _rateLimiterManager = GetRequiredService<RateLimiterManager>();
        _repository = GetRequiredService<IRepository<RateLimitPolicy, Guid>>();
    }

    /// <summary>测试共享同一内存库且生产种子会插入 global-default，每个用例先清空策略表保证独立。</summary>
    private async Task ClearPoliciesAsync()
    {
        foreach (var policy in await _repository.GetListAsync())
        {
            await _repository.HardDeleteAsync(policy, autoSave: true);
        }

        await _rateLimiterManager.ReloadAsync();
    }

    [Fact]
    public async Task Create_Should_Roundtrip_And_Resolve_Route_Over_Global()
    {
        await ClearPoliciesAsync();

        var global = await _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "global",
            Scope = RateLimitScope.Global,
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 100,
            WindowSeconds = 10
        });

        var route = await _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "route-a-policy",
            Scope = RateLimitScope.Route,
            RouteId = "route-a",
            Algorithm = RateLimitAlgorithm.SlidingWindow,
            PermitLimit = 5,
            WindowSeconds = 30,
            SegmentsPerWindow = 3,
            QueueLimit = 2
        });

        // GetAsync 字段往返
        var fetched = await _appService.GetAsync(route.Id);
        fetched.Name.ShouldBe("route-a-policy");
        fetched.Scope.ShouldBe(RateLimitScope.Route);
        fetched.RouteId.ShouldBe("route-a");
        fetched.Algorithm.ShouldBe(RateLimitAlgorithm.SlidingWindow);
        fetched.PermitLimit.ShouldBe(5);
        fetched.WindowSeconds.ShouldBe(30);
        fetched.SegmentsPerWindow.ShouldBe(3);
        fetched.QueueLimit.ShouldBe(2);
        fetched.IsEnabled.ShouldBeTrue();

        // 路由级优先全局；未匹配路由回退全局
        _rateLimiterManager.ResolveEffectivePolicy("route-a")!.Id.ShouldBe(route.Id);
        _rateLimiterManager.ResolveEffectivePolicy("other")!.Id.ShouldBe(global.Id);
    }

    [Fact]
    public async Task Disabled_Route_Policy_Should_Fallback_To_Global()
    {
        await ClearPoliciesAsync();

        var global = await _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "global",
            Scope = RateLimitScope.Global,
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 100,
            WindowSeconds = 10
        });
        await _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "route-a-policy",
            Scope = RateLimitScope.Route,
            RouteId = "route-a",
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 5,
            WindowSeconds = 10,
            IsEnabled = false
        });

        _rateLimiterManager.ResolveEffectivePolicy("route-a")!.Id.ShouldBe(global.Id);
    }

    [Fact]
    public async Task Create_Should_Reject_Second_Global()
    {
        await ClearPoliciesAsync();

        await _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "global",
            Scope = RateLimitScope.Global,
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 100,
            WindowSeconds = 10
        });

        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "global-2",
            Scope = RateLimitScope.Global,
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 50,
            WindowSeconds = 10
        }));

        ex.Code.ShouldBe(GatewayErrorCodes.GlobalRateLimitPolicyExists);
    }

    [Fact]
    public async Task Create_Should_Reject_Duplicate_Name()
    {
        await ClearPoliciesAsync();

        await _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "dup",
            Scope = RateLimitScope.Route,
            RouteId = "route-a",
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 10,
            WindowSeconds = 10
        });

        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "dup",
            Scope = RateLimitScope.Route,
            RouteId = "route-b",
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 10,
            WindowSeconds = 10
        }));

        ex.Code.ShouldBe(GatewayErrorCodes.DuplicateRateLimitPolicyName);
    }

    [Fact]
    public async Task Create_Should_Reject_Unknown_RouteId()
    {
        await ClearPoliciesAsync();

        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "orphan",
            Scope = RateLimitScope.Route,
            RouteId = "route-missing",
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 10,
            WindowSeconds = 10
        }));

        ex.Code.ShouldBe(GatewayErrorCodes.RouteNotFound);
    }

    [Fact]
    public async Task Create_Should_Reject_Second_Policy_For_Same_Route()
    {
        await ClearPoliciesAsync();

        await _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "route-a-1",
            Scope = RateLimitScope.Route,
            RouteId = "route-a",
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 10,
            WindowSeconds = 10
        });

        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "route-a-2",
            Scope = RateLimitScope.Route,
            RouteId = "route-a",
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 20,
            WindowSeconds = 10
        }));

        ex.Code.ShouldBe(GatewayErrorCodes.RouteRateLimitPolicyExists);
    }

    [Fact]
    public async Task Create_Should_Reject_TokenBucket_Missing_Params()
    {
        await ClearPoliciesAsync();

        var ex = await Should.ThrowAsync<BusinessException>(() => _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "bucket",
            Scope = RateLimitScope.Global,
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokensPerPeriod = 10,
            ReplenishmentPeriodSeconds = 5
        }));

        ex.Code.ShouldBe(GatewayErrorCodes.InvalidRateLimitParameters);
    }

    [Fact]
    public async Task Update_Should_Refresh_Snapshot()
    {
        await ClearPoliciesAsync();

        var policy = await _appService.CreateAsync(new RateLimitPolicyCreateUpdateDto
        {
            Name = "route-a-policy",
            Scope = RateLimitScope.Route,
            RouteId = "route-a",
            Algorithm = RateLimitAlgorithm.FixedWindow,
            PermitLimit = 10,
            WindowSeconds = 10
        });
        _rateLimiterManager.ResolveEffectivePolicy("route-a")!.PermitLimit.ShouldBe(10);

        await _appService.UpdateAsync(policy.Id, new RateLimitPolicyCreateUpdateDto
        {
            Name = "route-a-policy",
            Scope = RateLimitScope.Route,
            RouteId = "route-a",
            Algorithm = RateLimitAlgorithm.TokenBucket,
            TokenLimit = 100,
            TokensPerPeriod = 20,
            ReplenishmentPeriodSeconds = 5
        });

        var resolved = _rateLimiterManager.ResolveEffectivePolicy("route-a");
        resolved!.Algorithm.ShouldBe(RateLimitAlgorithm.TokenBucket);
        resolved.TokenLimit.ShouldBe(100);

        // 删除后无匹配策略 → 不限流
        await _appService.DeleteAsync(policy.Id);
        _rateLimiterManager.ResolveEffectivePolicy("route-a").ShouldBeNull();
    }
}
