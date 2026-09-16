using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.Gateway.RateLimiting;

[RemoteService(false)]
public class RateLimitPolicyAppService : ApplicationService, IRateLimitPolicyAppService
{
    private readonly IRepository<RateLimitPolicy, Guid> _repository;
    private readonly RateLimitPolicyManager _policyManager;
    private readonly RateLimiterManager _rateLimiterManager;

    public RateLimitPolicyAppService(
        IRepository<RateLimitPolicy, Guid> repository,
        RateLimitPolicyManager policyManager,
        RateLimiterManager rateLimiterManager)
    {
        _repository = repository;
        _policyManager = policyManager;
        _rateLimiterManager = rateLimiterManager;
    }

    public async Task<PagedResultDto<RateLimitPolicyDto>> GetListAsync(GetRateLimitPoliciesInput input)
    {
        var queryable = await _repository.GetQueryableAsync();
        queryable = queryable
            .WhereIf(!input.Keyword.IsNullOrWhiteSpace(),
                x => x.Name.Contains(input.Keyword!) || x.RouteId!.Contains(input.Keyword!))
            .WhereIf(input.Scope.HasValue, x => x.Scope == input.Scope!.Value)
            .WhereIf(!input.RouteId.IsNullOrWhiteSpace(), x => x.RouteId == input.RouteId);

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var items = await AsyncExecuter.ToListAsync(queryable
            .OrderBy(x => x.Scope).ThenBy(x => x.Name)
            .PageBy(input.SkipCount, input.MaxResultCount));

        return new PagedResultDto<RateLimitPolicyDto>(
            totalCount,
            items.Select(x => ObjectMapper.Map<RateLimitPolicy, RateLimitPolicyDto>(x)).ToList());
    }

    public async Task<RateLimitPolicyDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<RateLimitPolicy, RateLimitPolicyDto>(entity);
    }

    public async Task<RateLimitPolicyDto> CreateAsync(RateLimitPolicyCreateUpdateDto input)
    {
        var entity = new RateLimitPolicy(
            GuidGenerator.Create(),
            input.Name,
            input.Scope,
            input.RouteId,
            input.Algorithm,
            input.PermitLimit,
            input.WindowSeconds,
            input.SegmentsPerWindow,
            input.TokenLimit,
            input.TokensPerPeriod,
            input.ReplenishmentPeriodSeconds,
            input.QueueLimit,
            input.IsEnabled);

        await _policyManager.ValidateNewAsync(entity);
        await _repository.InsertAsync(entity, autoSave: true);
        await _rateLimiterManager.ReloadAsync();
        return ObjectMapper.Map<RateLimitPolicy, RateLimitPolicyDto>(entity);
    }

    public async Task<RateLimitPolicyDto> UpdateAsync(Guid id, RateLimitPolicyCreateUpdateDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Update(
            input.Name,
            input.Scope,
            input.RouteId,
            input.Algorithm,
            input.PermitLimit,
            input.WindowSeconds,
            input.SegmentsPerWindow,
            input.TokenLimit,
            input.TokensPerPeriod,
            input.ReplenishmentPeriodSeconds,
            input.QueueLimit);
        if (input.IsEnabled)
        {
            entity.Enable();
        }
        else
        {
            entity.Disable();
        }

        await _policyManager.ValidateUpdateAsync(entity);
        await _repository.UpdateAsync(entity, autoSave: true);
        await _rateLimiterManager.ReloadAsync();
        return ObjectMapper.Map<RateLimitPolicy, RateLimitPolicyDto>(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        await _repository.DeleteAsync(entity, autoSave: true);
        await _rateLimiterManager.ReloadAsync();
    }
}
