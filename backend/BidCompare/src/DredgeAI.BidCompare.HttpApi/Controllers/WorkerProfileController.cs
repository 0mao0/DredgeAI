using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DredgeAI.BidCompare.MeetingBot;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

[Area("meeting")]
[Route("api/meeting/workers")]
[Authorize]
public class WorkerProfileController : AbpControllerBase
{
    private readonly IWorkerProfileAppService _service;

    public WorkerProfileController(IWorkerProfileAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<List<WorkerDto>> List()
        => _service.GetListAsync();

    [HttpPost]
    public Task<WorkerDto> Create([FromBody] WorkerCreateInput input)
        => _service.CreateAsync(input);

    [HttpPost("recognize-id-card")]
    public async Task<IdCardRecognitionDto> RecognizeIdCard([FromForm] IFormFile image)
    {
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        return await _service.RecognizeIdCardAsync(ms.ToArray());
    }

    [HttpPost("import")]
    public async Task<int> Import([FromForm] IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return await _service.ImportAsync(ms.ToArray(), file.FileName);
    }

    [HttpPost("{id:guid}/face")]
    public async Task<WorkerDto> UpdateFace(Guid id, [FromForm] IFormFile image)
    {
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        return await _service.UpdateFaceAsync(id, ms.ToArray());
    }
}
