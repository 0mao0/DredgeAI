using System;
using System.Threading.Tasks;
using DredgeAI.Gateway.Proxying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.Gateway.Controllers;

[Area("gateway")]
[Route("api/gateway/proxy-routes")]
[Authorize]
public class ProxyRouteController : AbpControllerBase
{
    private readonly IProxyRouteAppService _appService;

    public ProxyRouteController(IProxyRouteAppService appService)
    {
        _appService = appService;
    }

    /// <summary>GET /api/gateway/proxy-routes 代理路由列表（分页）</summary>
    [HttpGet]
    public Task<PagedResultDto<ProxyRouteDto>> GetListAsync([FromQuery] GetProxyRoutesInput input)
        => _appService.GetListAsync(input);

    [HttpGet("{id}")]
    public Task<ProxyRouteDto> GetAsync(Guid id)
        => _appService.GetAsync(id);

    /// <summary>POST /api/gateway/proxy-routes 新增代理路由</summary>
    [HttpPost]
    public Task<ProxyRouteDto> CreateAsync([FromBody] ProxyRouteCreateUpdateDto input)
        => _appService.CreateAsync(input);

    /// <summary>PUT /api/gateway/proxy-routes/{id} 全量更新代理路由</summary>
    [HttpPut("{id}")]
    public Task<ProxyRouteDto> UpdateAsync(Guid id, [FromBody] ProxyRouteCreateUpdateDto input)
        => _appService.UpdateAsync(id, input);

    /// <summary>DELETE /api/gateway/proxy-routes/{id}</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _appService.DeleteAsync(id);
        return NoContent();
    }
}
