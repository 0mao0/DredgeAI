using System;
using System.Threading.Tasks;
using DredgeAI.BidCompare.ClauseTemplates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

[Area("compare")]
[Route("api/compare/clause-templates")]
[Authorize]
public class ClauseTemplateController : AbpControllerBase
{
    private readonly IClauseTemplateAppService _appService;

    public ClauseTemplateController(IClauseTemplateAppService appService)
    {
        _appService = appService;
    }

    /// <summary>GET /api/compare/clause-templates 个人条款库（分页）</summary>
    [HttpGet]
    public Task<PagedResultDto<ClauseTemplateDto>> GetListAsync([FromQuery] GetClauseTemplatesInput input)
        => _appService.GetListAsync(input);

    [HttpGet("{id}")]
    public Task<ClauseTemplateDto> GetAsync(Guid id)
        => _appService.GetAsync(id);

    /// <summary>POST /api/compare/clause-templates 新增条款模板</summary>
    [HttpPost]
    public Task<ClauseTemplateDto> CreateAsync([FromBody] ClauseTemplateCreateUpdateDto input)
        => _appService.CreateAsync(input);

    /// <summary>PUT /api/compare/clause-templates/{id}（补充路由，全量更新）</summary>
    [HttpPut("{id}")]
    public Task<ClauseTemplateDto> UpdateAsync(Guid id, [FromBody] ClauseTemplateCreateUpdateDto input)
        => _appService.UpdateAsync(id, input);

    /// <summary>DELETE /api/compare/clause-templates/{id}（补充路由）</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _appService.DeleteAsync(id);
        return NoContent();
    }
}
