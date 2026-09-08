using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DredgeAI.BidCompare.MeetingBot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>会议项目接口</summary>
[Authorize]
[Route("api/bidcompare/meeting-projects")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("会议项目")]
public class MeetingProjectController : BidCompareController
{
    private readonly IMeetingProjectAppService _service;

    public MeetingProjectController(IMeetingProjectAppService service)
    {
        _service = service;
    }

    /// <summary>GET /api/bidcompare/meeting-projects 会议项目列表</summary>
    /// <returns>会议项目列表</returns>
    [HttpGet]
    public Task<List<MeetingProjectDto>> GetListAsync()
        => _service.GetListAsync();

    /// <summary>POST /api/bidcompare/meeting-projects 创建会议项目</summary>
    /// <param name="input">项目创建参数</param>
    /// <returns>创建成功的会议项目</returns>
    [HttpPost]
    public Task<MeetingProjectDto> CreateAsync([FromBody] CreateMeetingProjectInput input)
        => _service.CreateAsync(input);

    /// <summary>PUT /api/bidcompare/meeting-projects/{id} 更新会议项目</summary>
    /// <param name="id">项目 ID</param>
    /// <param name="input">项目更新参数</param>
    /// <returns>更新后的会议项目</returns>
    [HttpPut("{id}")]
    public Task<MeetingProjectDto> UpdateAsync(Guid id, [FromBody] UpdateMeetingProjectInput input)
        => _service.UpdateAsync(id, input);

    /// <summary>DELETE /api/bidcompare/meeting-projects/{id} 删除会议项目</summary>
    /// <param name="id">项目 ID</param>
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id) => _service.DeleteAsync(id);

    /// <summary>GET /api/bidcompare/meeting-projects/{id} 会议项目详情</summary>
    /// <param name="id">项目 ID</param>
    /// <returns>会议项目详情</returns>
    [HttpGet("{id}")]
    public Task<MeetingProjectDto> GetAsync(Guid id)
        => _service.GetAsync(id);

    /// <summary>GET /api/bidcompare/meeting-projects/{id}/documents/{docId}/file 项目文档原文（PDF 预览）</summary>
    /// <param name="id">项目 ID</param>
    /// <param name="docId">文档 ID</param>
    /// <returns>文档文件流（支持 Range 请求）</returns>
    [HttpGet("{id}/documents/{docId}/file")]
    public async Task<IActionResult> GetDocumentFileAsync(Guid id, string docId)
    {
        var result = await _service.GetDocumentFileAsync(id, docId);
        return new FileStreamResult(result.Content, result.ContentType)
        {
            EnableRangeProcessing = true // pdf.js 流式加载依赖 Range 请求
        };
    }

    /// <summary>GET /api/bidcompare/meeting-projects/{id}/documents/{docId}/content 项目文档解析内容</summary>
    /// <param name="id">项目 ID</param>
    /// <param name="docId">文档 ID</param>
    /// <returns>文档解析内容</returns>
    [HttpGet("{id}/documents/{docId}/content")]
    public Task<MeetingBot.MeetingProjectDocumentContentDto> GetDocumentContentAsync(Guid id, string docId)
        => _service.GetDocumentContentAsync(id, docId);

    /// <summary>POST /api/bidcompare/meeting-projects/{id}/extract 触发提取项目文档</summary>
    /// <param name="id">项目 ID</param>
    /// <returns>提取后的会议项目</returns>
    [HttpPost("{id}/extract")]
    public Task<MeetingProjectDto> ExtractAsync(Guid id)
        => _service.ExtractAsync(id);

    /// <summary>POST /api/bidcompare/meeting-projects/suggest-name 根据文档内容建议项目名</summary>
    /// <param name="input">建议名称输入（含文档 ID）</param>
    /// <returns>建议的项目名</returns>
    [HttpPost("suggest-name")]
    public Task<MeetingBot.ProjectNameSuggestionDto> SuggestNameAsync([FromBody] MeetingBot.SuggestProjectNameInput input)
        => _service.SuggestNameAsync(input.DocId);
}
