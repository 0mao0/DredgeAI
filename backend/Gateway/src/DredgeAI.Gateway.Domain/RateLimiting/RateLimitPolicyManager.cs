using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using DredgeAI.Gateway.Proxying;

namespace DredgeAI.Gateway.RateLimiting;

/// <summary>限流策略领域服务：写入前的业务校验（名称唯一、全局/路由级单条、路由存在、算法参数齐全）。</summary>
public class RateLimitPolicyManager : DomainService
{
    private readonly IRepository<RateLimitPolicy, Guid> _policyRepository;
    private readonly IRepository<ProxyRoute, Guid> _routeRepository;

    public RateLimitPolicyManager(
        IRepository<RateLimitPolicy, Guid> policyRepository,
        IRepository<ProxyRoute, Guid> routeRepository)
    {
        _policyRepository = policyRepository;
        _routeRepository = routeRepository;
    }

    public async Task ValidateNewAsync(RateLimitPolicy policy)
    {
        await ValidateAsync(policy, excludeId: null);
    }

    public async Task ValidateUpdateAsync(RateLimitPolicy policy)
    {
        await ValidateAsync(policy, policy.Id);
    }

    private async Task ValidateAsync(RateLimitPolicy policy, Guid? excludeId)
    {
        if (await _policyRepository.AnyAsync(x => x.Name == policy.Name && x.Id != excludeId))
        {
            throw new BusinessException(GatewayErrorCodes.DuplicateRateLimitPolicyName);
        }

        if (policy.Scope == RateLimitScope.Global)
        {
            if (await _policyRepository.AnyAsync(x => x.Scope == RateLimitScope.Global && x.Id != excludeId))
            {
                throw new BusinessException(GatewayErrorCodes.GlobalRateLimitPolicyExists);
            }
        }
        else
        {
            if (policy.RouteId is null)
            {
                throw new BusinessException(GatewayErrorCodes.InvalidRateLimitParameters);
            }

            if (!await _routeRepository.AnyAsync(x => x.RouteId == policy.RouteId))
            {
                throw new BusinessException(GatewayErrorCodes.RouteNotFound);
            }

            if (await _policyRepository.AnyAsync(x =>
                    x.Scope == RateLimitScope.Route && x.RouteId == policy.RouteId && x.Id != excludeId))
            {
                throw new BusinessException(GatewayErrorCodes.RouteRateLimitPolicyExists);
            }
        }

        ValidateAlgorithmParameters(policy);
    }

    private static void ValidateAlgorithmParameters(RateLimitPolicy policy)
    {
        if (policy.QueueLimit < 0)
        {
            throw new BusinessException(GatewayErrorCodes.InvalidRateLimitParameters);
        }

        var valid = policy.Algorithm switch
        {
            RateLimitAlgorithm.FixedWindow =>
                policy.PermitLimit > 0 && policy.WindowSeconds > 0,
            RateLimitAlgorithm.SlidingWindow =>
                policy.PermitLimit > 0 && policy.WindowSeconds > 0 && policy.SegmentsPerWindow > 0,
            RateLimitAlgorithm.TokenBucket =>
                policy.TokenLimit > 0 && policy.TokensPerPeriod > 0 && policy.ReplenishmentPeriodSeconds > 0,
            _ => false
        };

        if (!valid)
        {
            throw new BusinessException(GatewayErrorCodes.InvalidRateLimitParameters);
        }
    }
}
