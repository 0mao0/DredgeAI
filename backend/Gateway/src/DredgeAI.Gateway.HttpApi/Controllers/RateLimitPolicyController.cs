using System;
using System.Threading.Tasks;
using DredgeAI.Gateway.Permissions;
using DredgeAI.Gateway.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.Gateway.Controllers;

[Area("gateway")]
[Route("api/gateway/rate-limit-policies")]
[Authorize(GatewayPermissions.RateLimitPolicies.Default)]
public class RateLimitPolicyController : AbpControllerBase
{
    private readonly IRateLimitPolicyAppService _appService;

    public RateLimitPolicyController(IRateLimitPolicyAppService appService)
    {
        _appService = appService;
    }

    /// <summary>GET /api/gateway/rate-limit-policies 限流策略列表（分页）</summary>
    [HttpGet]
    public Task<PagedResultDto<RateLimitPolicyDto>> GetListAsync([FromQuery] GetRateLimitPoliciesInput input)
        => _appService.GetListAsync(input);

    [HttpGet("{id}")]
    public Task<RateLimitPolicyDto> GetAsync(Guid id)
        => _appService.GetAsync(id);

    /// <summary>POST /api/gateway/rate-limit-policies 新增限流策略</summary>
    [HttpPost]
    [Authorize(GatewayPermissions.RateLimitPolicies.Create)]
    public Task<RateLimitPolicyDto> CreateAsync([FromBody] RateLimitPolicyCreateUpdateDto input)
        => _appService.CreateAsync(input);

    /// <summary>PUT /api/gateway/rate-limit-policies/{id} 全量更新限流策略</summary>
    [HttpPut("{id}")]
    [Authorize(GatewayPermissions.RateLimitPolicies.Update)]
    public Task<RateLimitPolicyDto> UpdateAsync(Guid id, [FromBody] RateLimitPolicyCreateUpdateDto input)
        => _appService.UpdateAsync(id, input);

    /// <summary>DELETE /api/gateway/rate-limit-policies/{id}</summary>
    [HttpDelete("{id}")]
    [Authorize(GatewayPermissions.RateLimitPolicies.Delete)]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _appService.DeleteAsync(id);
        return NoContent();
    }
}
