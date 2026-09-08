using System;
using System.Threading.Tasks;
using DredgeAI.Gateway.Proxying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.Gateway.Controllers;

[Area("gateway")]
[Route("api/gateway/proxy-clusters")]
[Authorize]
public class ProxyClusterController : AbpControllerBase
{
    private readonly IProxyClusterAppService _appService;

    public ProxyClusterController(IProxyClusterAppService appService)
    {
        _appService = appService;
    }

    /// <summary>GET /api/gateway/proxy-clusters 代理集群列表（不分页）</summary>
    [HttpGet]
    public Task<ListResultDto<ProxyClusterDto>> GetListAsync()
        => _appService.GetListAsync();

    [HttpGet("{id}")]
    public Task<ProxyClusterDto> GetAsync(Guid id)
        => _appService.GetAsync(id);

    /// <summary>POST /api/gateway/proxy-clusters 新增代理集群</summary>
    [HttpPost]
    public Task<ProxyClusterDto> CreateAsync([FromBody] ProxyClusterCreateUpdateDto input)
        => _appService.CreateAsync(input);

    /// <summary>PUT /api/gateway/proxy-clusters/{id} 全量更新代理集群</summary>
    [HttpPut("{id}")]
    public Task<ProxyClusterDto> UpdateAsync(Guid id, [FromBody] ProxyClusterCreateUpdateDto input)
        => _appService.UpdateAsync(id, input);

    /// <summary>DELETE /api/gateway/proxy-clusters/{id}</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _appService.DeleteAsync(id);
        return NoContent();
    }
}
