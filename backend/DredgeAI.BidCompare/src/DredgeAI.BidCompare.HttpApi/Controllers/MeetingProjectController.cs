using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DredgeAI.BidCompare.MeetingBot;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

[Area("meeting")]
[Route("api/meeting/projects")]
public class MeetingProjectController : AbpControllerBase
{
    private readonly IMeetingProjectAppService _service;

    public MeetingProjectController(IMeetingProjectAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<List<MeetingProjectDto>> List()
        => _service.GetListAsync();

    [HttpPost]
    public Task<MeetingProjectDto> Create([FromBody] CreateMeetingProjectInput input)
        => _service.CreateAsync(input);

    [HttpPut("{id:guid}")]
    public Task<MeetingProjectDto> Update(Guid id, [FromBody] UpdateMeetingProjectInput input)
        => _service.UpdateAsync(id, input);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }

    [HttpGet("{id:guid}")]
    public Task<MeetingProjectDto> Get(Guid id)
        => _service.GetAsync(id);

    [HttpGet("{id:guid}/documents/{docId}/file")]
    public async Task<IActionResult> GetDocumentFile(Guid id, string docId)
    {
        var result = await _service.GetDocumentFileAsync(id, docId);
        return new FileStreamResult(result.Content, result.ContentType)
        {
            EnableRangeProcessing = true // pdf.js 流式加载依赖 Range 请求
        };
    }

    [HttpGet("{id:guid}/documents/{docId}/content")]
    public Task<MeetingBot.MeetingProjectDocumentContentDto> GetDocumentContent(Guid id, string docId)
        => _service.GetDocumentContentAsync(id, docId);

    [HttpPost("{id:guid}/extract")]
    public Task<MeetingProjectDto> Extract(Guid id)
        => _service.ExtractAsync(id);

    [HttpPost("suggest-name")]
    public Task<MeetingBot.ProjectNameSuggestionDto> SuggestName([FromBody] MeetingBot.SuggestProjectNameInput input)
        => _service.SuggestNameAsync(input.DocId);
}
