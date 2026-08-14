using System;
using System.Collections.Generic;
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

    /// <summary>GET /api/compare/tasks/{id}/documents 任务文档列表（前端详情/历史用）</summary>
    [HttpGet("{id}/documents")]
    public Task<List<CompareDocumentDto>> GetDocumentsAsync(Guid id)
        => _appService.GetDocumentsAsync(id);

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

    /// <summary>POST /api/compare/tasks/{id}/reparse 重新解析失败文档（旧路由兼容，等价于缺省全部失败文档）</summary>
    [HttpPost("{id}/reparse")]
    public Task<CompareTaskDto> ReparseAsync(Guid id)
        => _appService.ReparseAsync(id, new CompareTasks.ReparseDocumentsInput());

    /// <summary>POST /api/compare/tasks/{id}/documents/reparse 重新解析失败文档（v2 §8.2，body.docIds 缺省为全部失败文档）</summary>
    [HttpPost("{id}/documents/reparse")]
    public Task<CompareTaskDto> ReparseDocumentsAsync(Guid id, [FromBody] CompareTasks.ReparseDocumentsInput? input)
        => _appService.ReparseAsync(id, input ?? new CompareTasks.ReparseDocumentsInput());

    /// <summary>POST /api/compare/tasks/{id}/compare/retry 重新对比（v2 §8.2，body.pairIds 缺省为全量；analyzing 时 409）</summary>
    [HttpPost("{id}/compare/retry")]
    public Task<CompareTaskDto> RetryCompareAsync(Guid id, [FromBody] CompareTasks.RetryCompareInput? input)
        => _appService.RetryCompareAsync(id, input ?? new CompareTasks.RetryCompareInput());

    /// <summary>PUT /api/compare/tasks/{id}/name 编辑项目名（v2 §3.3，置 nameEditedByUser = true）</summary>
    [HttpPut("{id}/name")]
    public Task<CompareTaskDto> UpdateNameAsync(Guid id, [FromBody] CompareTasks.UpdateCompareTaskNameInput input)
        => _appService.UpdateNameAsync(id, input);

    /// <summary>POST /api/compare/tasks/{id}/documents 上传文档（标书/招标文件，区分 role）</summary>
    [HttpPost("{id}/documents")]
    [RequestSizeLimit(200 * 1024 * 1024)] // 单份标书 100~500 页 PDF，放宽到 200MB
    public async Task<CompareDocumentDto> UploadDocumentAsync(Guid id, [FromForm] UploadDocumentForm form)
    {
        await using var stream = form.File.OpenReadStream();
        return await _appService.UploadDocumentAsync(id, form.Role, form.File.FileName, stream);
    }

    /// <summary>POST /api/compare/tasks/{id}/documents/parse 上传完成后批量并发解析（v2 修订：不再逐份入队）</summary>
    [HttpPost("{id}/documents/parse")]
    public Task<CompareTaskDto> StartParsingAsync(Guid id)
        => _appService.StartParsingAsync(id);

    /// <summary>GET /api/compare/tasks/{id}/documents/{docId}/file 文档原文（PDF Viewer 预览用）</summary>
    [HttpGet("{id}/documents/{docId}/file")]
    public async Task<IActionResult> GetDocumentFileAsync(Guid id, Guid docId)
    {
        var result = await _appService.GetDocumentFileAsync(id, docId);
        return new FileStreamResult(result.Content, result.ContentType)
        {
            EnableRangeProcessing = true, // pdf.js 流式加载依赖 Range 请求
        };
    }

    /// <summary>GET /api/compare/tasks/{id}/ir/{docId} 某文档的 IR（前端对比视图画 bbox 用）</summary>
    [HttpGet("{id}/ir/{docId}")]
    public Task<Ir.DocumentIrDto> GetDocumentIrAsync(Guid id, Guid docId)
        => _appService.GetDocumentIrAsync(id, docId);

    /// <summary>GET /api/compare/tasks/{id}/evidences 证据项列表（按类型/严重度/文档对过滤）</summary>
    [HttpGet("{id}/evidences")]
    public Task<PagedResultDto<Evidences.EvidenceDto>> GetEvidencesAsync(Guid id, [FromQuery] Evidences.GetEvidenceListInput input)
        => _appService.GetEvidencesAsync(id, input);

    /// <summary>GET /api/compare/tasks/{id}/matrix 两两相似度矩阵（N×N，热力图用）</summary>
    [HttpGet("{id}/matrix")]
    public Task<Analysis.SimilarityMatrixDto> GetMatrixAsync(Guid id)
        => _appService.GetMatrixAsync(id);

    /// <summary>POST /api/compare/tasks/{id}/clauses/extract 触发从招标文件提取条款草案</summary>
    [HttpPost("{id}/clauses/extract")]
    public Task<List<Clauses.ClauseDto>> ExtractClausesAsync(Guid id)
        => _appService.ExtractClausesAsync(id);

    /// <summary>PUT /api/compare/tasks/{id}/clauses 确认后的条款清单（锁定快照）</summary>
    [HttpPut("{id}/clauses")]
    public Task<CompareTaskDto> ConfirmClausesAsync(Guid id, [FromBody] Clauses.ConfirmClausesInput input)
        => _appService.ConfirmClausesAsync(id, input);

    /// <summary>GET /api/compare/tasks/{id}/report 结构化报告 JSON</summary>
    [HttpGet("{id}/report")]
    public Task<Reports.CompareReportDto> GetReportAsync(Guid id)
        => _appService.GetReportAsync(id);

    /// <summary>POST /api/compare/tasks/{id}/export 生成导出文件 { format } → 异步 → 下载 URL</summary>
    [HttpPost("{id}/export")]
    public Task<Exports.ExportJobDto> RequestExportAsync(Guid id, [FromBody] Exports.ExportRequestDto input)
        => _appService.RequestExportAsync(id, input);

    /// <summary>GET /api/compare/tasks/{id}/exports/{jobId}（补充路由：导出轮询，spec §6.2）</summary>
    [HttpGet("{id}/exports/{jobId}")]
    public Task<Exports.ExportJobDto> GetExportJobAsync(Guid id, Guid jobId)
        => _appService.GetExportJobAsync(id, jobId);
}

public class UploadDocumentForm
{
    [Required]
    public IFormFile File { get; set; } = default!;

    /// <summary>0=Bid 标书（默认），1=Tender 招标文件。</summary>
    [FromForm]
    public DocumentRole Role { get; set; } = DocumentRole.Bid;
}
