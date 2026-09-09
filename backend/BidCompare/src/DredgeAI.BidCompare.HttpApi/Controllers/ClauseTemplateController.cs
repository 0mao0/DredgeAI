using System;
using System.Threading.Tasks;
using DredgeAI.BidCompare.ClauseTemplates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>条款模板接口</summary>
[Authorize]
[Route("api/bidcompare/clause-templates")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("条款模板")]
public class ClauseTemplateController : BidCompareController, IClauseTemplateAppService
{
    private readonly IClauseTemplateAppService _appService;

    public ClauseTemplateController(IClauseTemplateAppService appService)
    {
        _appService = appService;
    }

    /// <summary>GET /api/bidcompare/clause-templates 个人条款库（分页）</summary>
    /// <param name="input">查询条件</param>
    /// <returns>分页的条款模板列表</returns>
    [HttpGet]
    public Task<PagedResultDto<ClauseTemplateDto>> GetListAsync([FromQuery] GetClauseTemplatesInput input)
        => _appService.GetListAsync(input);

    /// <summary>GET /api/bidcompare/clause-templates/{id} 按 ID 获取单个条款模板</summary>
    /// <param name="id">条款模板 ID</param>
    /// <returns>条款模板详情</returns>
    [HttpGet("{id}")]
    public Task<ClauseTemplateDto> GetAsync(Guid id)
        => _appService.GetAsync(id);

    /// <summary>POST /api/bidcompare/clause-templates 新增条款模板</summary>
    /// <param name="input">条款模板创建参数</param>
    /// <returns>创建成功的条款模板</returns>
    [HttpPost]
    public Task<ClauseTemplateDto> CreateAsync([FromBody] ClauseTemplateCreateUpdateDto input)
        => _appService.CreateAsync(input);

    /// <summary>PUT /api/bidcompare/clause-templates/{id} 更新条款模板（全量更新）</summary>
    /// <param name="id">条款模板 ID</param>
    /// <param name="input">条款模板更新参数</param>
    /// <returns>更新后的条款模板</returns>
    [HttpPut("{id}")]
    public Task<ClauseTemplateDto> UpdateAsync(Guid id, [FromBody] ClauseTemplateCreateUpdateDto input)
        => _appService.UpdateAsync(id, input);

    /// <summary>DELETE /api/bidcompare/clause-templates/{id} 删除条款模板</summary>
    /// <param name="id">条款模板 ID</param>
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id) => _appService.DeleteAsync(id);
}
