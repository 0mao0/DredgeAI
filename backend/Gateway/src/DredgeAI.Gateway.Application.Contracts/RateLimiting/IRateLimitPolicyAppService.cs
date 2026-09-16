using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace DredgeAI.Gateway.RateLimiting;

public interface IRateLimitPolicyAppService : IApplicationService
{
    Task<PagedResultDto<RateLimitPolicyDto>> GetListAsync(GetRateLimitPoliciesInput input);

    Task<RateLimitPolicyDto> GetAsync(Guid id);

    Task<RateLimitPolicyDto> CreateAsync(RateLimitPolicyCreateUpdateDto input);

    Task<RateLimitPolicyDto> UpdateAsync(Guid id, RateLimitPolicyCreateUpdateDto input);

    Task DeleteAsync(Guid id);
}
