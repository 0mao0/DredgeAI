using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Drafts;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>比标草稿接口</summary>
[Authorize]
[Route("api/bidcompare/compare-drafts")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("比标草稿")]
public class CompareDraftController : BidCompareController
{
    private readonly ICompareDraftAppService _appService;

    public CompareDraftController(ICompareDraftAppService appService)
    {
        _appService = appService;
    }

    /// <summary>GET /api/bidcompare/compare-drafts/{draftId} 会话已上传文件（刷新/续传进度恢复用）</summary>
    /// <param name="draftId">草稿会话 ID</param>
    /// <returns>已上传的文档列表</returns>
    [HttpGet("{draftId}")]
    public Task<List<CompareDraftDocumentDto>> GetDocumentsAsync(Guid draftId)
        => _appService.GetDocumentsAsync(draftId);

    /// <summary>POST /api/bidcompare/compare-drafts/{draftId}/documents 上传暂存文件（标书/招标，区分 role，不触发解析）</summary>
    /// <param name="draftId">草稿会话 ID</param>
    /// <param name="form">上传文档表单（文件 + 角色）</param>
    /// <returns>上传成功的文档</returns>
    [HttpPost("{draftId}/documents")]
    [RequestSizeLimit(200 * 1024 * 1024)] // 与任务文档上传保持一致，放宽到 200MB
    public async Task<CompareDraftDocumentDto> UploadDocumentAsync(Guid draftId, [FromForm] UploadDraftDocumentForm form)
    {
        await using var stream = form.File.OpenReadStream();
        return await _appService.UploadDocumentAsync(draftId, form.Role, form.File.FileName, stream);
    }

    /// <summary>DELETE /api/bidcompare/compare-drafts/{draftId}/documents/{docId} 删除会话内单个文件</summary>
    /// <param name="draftId">草稿会话 ID</param>
    /// <param name="docId">文档 ID</param>
    [HttpDelete("{draftId}/documents/{docId}")]
    public Task DeleteDocumentAsync(Guid draftId, Guid docId) => _appService.DeleteDocumentAsync(draftId, docId);

    /// <summary>DELETE /api/bidcompare/compare-drafts/{draftId} 清空整个会话（文件 + 记录）</summary>
    /// <param name="draftId">草稿会话 ID</param>
    [HttpDelete("{draftId}")]
    public Task DeleteDraftAsync(Guid draftId) => _appService.DeleteDraftAsync(draftId);
}

public class UploadDraftDocumentForm
{
    [Required]
    public IFormFile File { get; set; } = default!;

    /// <summary>0=Bid 标书（默认），1=Tender 招标文件。</summary>
    [FromForm]
    public DocumentRole Role { get; set; } = DocumentRole.Bid;
}
