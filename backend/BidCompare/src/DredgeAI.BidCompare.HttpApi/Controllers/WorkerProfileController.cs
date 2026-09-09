using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DredgeAI.BidCompare.MeetingBot;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>工人档案接口</summary>
[Authorize]
[Route("api/bidcompare/meeting-workers")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("工人档案")]
public class WorkerProfileController : BidCompareController
{
    private readonly IWorkerProfileAppService _service;

    public WorkerProfileController(IWorkerProfileAppService service)
    {
        _service = service;
    }

    /// <summary>GET /api/bidcompare/meeting-workers 工人档案列表</summary>
    /// <returns>工人档案列表</returns>
    [HttpGet]
    public Task<List<WorkerDto>> GetListAsync()
        => _service.GetListAsync();

    /// <summary>POST /api/bidcompare/meeting-workers 创建工人档案</summary>
    /// <param name="input">工人创建参数</param>
    /// <returns>创建成功的工人档案</returns>
    [HttpPost]
    public Task<WorkerDto> CreateAsync([FromBody] WorkerCreateInput input)
        => _service.CreateAsync(input);

    /// <summary>POST /api/bidcompare/meeting-workers/recognize-id-card 身份证 OCR 识别建档</summary>
    /// <param name="image">身份证图片文件</param>
    /// <returns>识别结果</returns>
    [HttpPost("recognize-id-card")]
    public async Task<IdCardRecognitionDto> RecognizeIdCardAsync(IFormFile image)
    {
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        return await _service.RecognizeIdCardAsync(ms.ToArray());
    }

    /// <summary>POST /api/bidcompare/meeting-workers/import 批量导入工人档案</summary>
    /// <param name="file">导入文件</param>
    /// <returns>导入成功的数量</returns>
    [HttpPost("import")]
    public async Task<int> ImportAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return await _service.ImportAsync(ms.ToArray(), file.FileName);
    }

    /// <summary>POST /api/bidcompare/meeting-workers/{id}/face 更新工人人脸照片</summary>
    /// <param name="id">工人 ID</param>
    /// <param name="image">人脸图片文件</param>
    /// <returns>更新后的工人档案</returns>
    [HttpPost("{id}/face")]
    public async Task<WorkerDto> UpdateFaceAsync(Guid id, IFormFile image)
    {
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        return await _service.UpdateFaceAsync(id, ms.ToArray());
    }
}
