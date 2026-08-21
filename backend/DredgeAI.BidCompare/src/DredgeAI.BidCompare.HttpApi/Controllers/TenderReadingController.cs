using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DredgeAI.BidCompare.TenderReadings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

[Area("tender-read")]
[Route("api/tender-read/tasks")]
public class TenderReadingController : AbpControllerBase
{
    private readonly ITenderReadingAppService _appService;

    public TenderReadingController(ITenderReadingAppService appService)
    {
        _appService = appService;
    }

    /// <summary>POST /api/tender-read/tasks 创建读标任务。</summary>
    [HttpPost]
    public Task<TenderReadingTaskDto> CreateAsync([FromBody] CreateTenderReadingTaskDto input)
        => _appService.CreateAsync(input);

    /// <summary>GET /api/tender-read/tasks/{id} 任务详情 + 状态 + 进度。</summary>
    [HttpGet("{id}")]
    public Task<TenderReadingTaskDto> GetAsync(Guid id)
        => _appService.GetAsync(id);

    /// <summary>GET /api/tender-read/tasks 分页查询任务列表。</summary>
    [HttpGet]
    public Task<PagedResultDto<TenderReadingTaskDto>> GetListAsync([FromQuery] GetTenderReadingTasksInput input)
        => _appService.GetListAsync(input);

    /// <summary>PUT /api/tender-read/tasks/{id}/name 编辑项目名 / 编号。</summary>
    [HttpPut("{id}/name")]
    public Task<TenderReadingTaskDto> UpdateAsync(Guid id, [FromBody] UpdateTenderReadingTaskInput input)
        => _appService.UpdateAsync(id, input);

    /// <summary>DELETE /api/tender-read/tasks/{id} 删除任务。</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _appService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>GET /api/tender-read/tasks/{id}/documents 任务文档列表。</summary>
    [HttpGet("{id}/documents")]
    public Task<System.Collections.Generic.List<TenderReadingDocumentDto>> GetDocumentsAsync(Guid id)
        => _appService.GetDocumentsAsync(id);

    /// <summary>POST /api/tender-read/tasks/{id}/document 上传招标文件。</summary>
    [HttpPost("{id}/document")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<TenderReadingDocumentDto> UploadDocumentAsync(Guid id, [FromForm] UploadTenderDocumentForm form)
    {
        await using var stream = form.File.OpenReadStream();
        return await _appService.UploadDocumentAsync(id, form.File.FileName, stream);
    }

    /// <summary>POST /api/tender-read/tasks/{id}/parse 触发解析。</summary>
    [HttpPost("{id}/parse")]
    public Task<TenderReadingTaskDto> StartParsingAsync(Guid id)
        => _appService.StartParsingAsync(id);

    /// <summary>POST /api/tender-read/tasks/{id}/reparse 重新解析失败文档。</summary>
    [HttpPost("{id}/reparse")]
    public Task<TenderReadingTaskDto> ReparseAsync(Guid id)
        => _appService.ReparseAsync(id);

    /// <summary>GET /api/tender-read/tasks/{id}/outline 目录树。</summary>
    [HttpGet("{id}/outline")]
    public Task<System.Collections.Generic.List<TenderReadingOutlineNodeDto>> GetOutlineAsync(Guid id)
        => _appService.GetOutlineAsync(id);

    /// <summary>GET /api/tender-read/tasks/{id}/baseline 完整基准库。</summary>
    [HttpGet("{id}/baseline")]
    public Task<TenderReadingBaselineDto> GetBaselineAsync(Guid id)
        => _appService.GetBaselineAsync(id);

    /// <summary>GET /api/tender-read/tasks/{id}/baseline/{category} 按类目查询字段。</summary>
    [HttpGet("{id}/baseline/{category}")]
    public Task<System.Collections.Generic.List<BaselineFieldDto>> GetBaselineByCategoryAsync(Guid id, BaselineCategory category)
        => _appService.GetBaselineByCategoryAsync(id, category);

    /// <summary>GET /api/tender-read/tasks/{id}/source/{fieldId} 字段原文锚点。</summary>
    [HttpGet("{id}/source/{fieldId}")]
    public Task<System.Collections.Generic.List<SourceRefDto>> GetSourceAsync(Guid id, Guid fieldId)
        => _appService.GetSourceAsync(id, fieldId);

    /// <summary>PUT /api/tender-read/tasks/{id}/fields/{fieldId} 人工确认 / 修改字段。</summary>
    [HttpPut("{id}/fields/{fieldId}")]
    public Task<BaselineFieldDto> UpdateFieldAsync(Guid id, Guid fieldId, [FromBody] UpdateBaselineFieldInput input)
        => _appService.UpdateFieldAsync(id, fieldId, input);

    /// <summary>POST /api/tender-read/tasks/{id}/re-extract 重新抽取指定类目（缺省全量），后台执行，返回任务快照。</summary>
    [HttpPost("{id}/re-extract")]
    public Task<TenderReadingTaskDto> ReExtractAsync(Guid id, [FromBody] ReExtractBaselineInput? input)
        => _appService.ReExtractAsync(id, input ?? new ReExtractBaselineInput());

    /// <summary>GET /api/tender-read/tasks/{id}/document/file 文档原文流（PDF 预览）。</summary>
    [HttpGet("{id}/document/file")]
    public async Task<IActionResult> GetDocumentFileAsync(Guid id)
    {
        var result = await _appService.GetDocumentFileAsync(id);
        return new FileStreamResult(result.Content, result.ContentType)
        {
            EnableRangeProcessing = true // pdf.js 流式加载依赖 Range 请求
        };
    }

    /// <summary>GET /api/tender-read/tasks/{id}/export 导出基准库 JSON。</summary>
    [HttpGet("{id}/export")]
    public Task<TenderReadingBaselineDto> ExportBaselineAsync(Guid id)
        => _appService.ExportBaselineAsync(id);
}

public class UploadTenderDocumentForm
{
    [Required]
    public IFormFile File { get; set; } = default!;
}
