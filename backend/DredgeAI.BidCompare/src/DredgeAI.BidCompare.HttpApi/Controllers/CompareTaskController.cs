using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

[Area("compare")]
[Route("api/compare/tasks")]
public class CompareTaskController : AbpControllerBase
{
    private readonly ICompareTaskAppService _appService;

    public CompareTaskController(ICompareTaskAppService appService)
    {
        _appService = appService;
    }

    /// <summary>POST /api/compare/tasks 创建任务（含条款清单快照）</summary>
    [HttpPost]
    public Task<CompareTaskDto> CreateAsync([FromBody] CreateCompareTaskDto input)
        => _appService.CreateAsync(input);

    /// <summary>GET /api/compare/tasks/{id} 任务详情 + 状态机状态 + 各阶段进度</summary>
    [HttpGet("{id}")]
    public Task<CompareTaskDto> GetAsync(Guid id)
        => _appService.GetAsync(id);

    /// <summary>GET /api/compare/tasks 任务列表（分页，PagedResultDto）</summary>
    [HttpGet]
    public Task<PagedResultDto<CompareTaskDto>> GetListAsync([FromQuery] GetCompareTasksInput input)
        => _appService.GetListAsync(input);

    /// <summary>DELETE /api/compare/tasks/{id}（补充路由，spec §7.1 操作列删除）</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAsync(Guid id)
    {
        await _appService.DeleteAsync(id);
        return NoContent();
    }

    /// <summary>POST /api/compare/tasks/{id}/documents 上传文档（标书/招标文件，区分 role）</summary>
    [HttpPost("{id}/documents")]
    [RequestSizeLimit(200 * 1024 * 1024)] // 单份标书 100~500 页 PDF，放宽到 200MB
    public async Task<CompareDocumentDto> UploadDocumentAsync(Guid id, [FromForm] UploadDocumentForm form)
    {
        await using var stream = form.File.OpenReadStream();
        return await _appService.UploadDocumentAsync(id, form.Role, form.File.FileName, stream);
    }
}

public class UploadDocumentForm
{
    [Required]
    public IFormFile File { get; set; } = default!;

    /// <summary>0=Bid 标书（默认），1=Tender 招标文件。</summary>
    [FromForm]
    public DocumentRole Role { get; set; } = DocumentRole.Bid;
}
