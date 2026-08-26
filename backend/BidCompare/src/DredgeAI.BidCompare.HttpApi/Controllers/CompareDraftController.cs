using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Drafts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

[Area("compare")]
[Route("api/compare/drafts")]
[Authorize]
public class CompareDraftController : AbpControllerBase
{
    private readonly ICompareDraftAppService _appService;

    public CompareDraftController(ICompareDraftAppService appService)
    {
        _appService = appService;
    }

    /// <summary>GET /api/compare/drafts/{draftId} 会话已上传文件（刷新/续传进度恢复用）</summary>
    [HttpGet("{draftId}")]
    public Task<List<CompareDraftDocumentDto>> GetDocumentsAsync(Guid draftId)
        => _appService.GetDocumentsAsync(draftId);

    /// <summary>POST /api/compare/drafts/{draftId}/documents 上传暂存文件（标书/招标，区分 role，不触发解析）</summary>
    [HttpPost("{draftId}/documents")]
    [RequestSizeLimit(200 * 1024 * 1024)] // 与任务文档上传保持一致，放宽到 200MB
    public async Task<CompareDraftDocumentDto> UploadDocumentAsync(Guid draftId, [FromForm] UploadDraftDocumentForm form)
    {
        await using var stream = form.File.OpenReadStream();
        return await _appService.UploadDocumentAsync(draftId, form.Role, form.File.FileName, stream);
    }

    /// <summary>DELETE /api/compare/drafts/{draftId}/documents/{docId} 删除会话内单个文件</summary>
    [HttpDelete("{draftId}/documents/{docId}")]
    public async Task<IActionResult> DeleteDocumentAsync(Guid draftId, Guid docId)
    {
        await _appService.DeleteDocumentAsync(draftId, docId);
        return NoContent();
    }

    /// <summary>DELETE /api/compare/drafts/{draftId} 清空整个会话（文件 + 记录）</summary>
    [HttpDelete("{draftId}")]
    public async Task<IActionResult> DeleteDraftAsync(Guid draftId)
    {
        await _appService.DeleteDraftAsync(draftId);
        return NoContent();
    }
}

public class UploadDraftDocumentForm
{
    [Required]
    public IFormFile File { get; set; } = default!;

    /// <summary>0=Bid 标书（默认），1=Tender 招标文件。</summary>
    [FromForm]
    public DocumentRole Role { get; set; } = DocumentRole.Bid;
}
