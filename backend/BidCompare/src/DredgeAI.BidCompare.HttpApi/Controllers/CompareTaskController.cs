using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DredgeAI.BidCompare.CompareTasks;
using DredgeAI.BidCompare.Documents;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>比标任务接口</summary>
[Authorize]
[Route("api/bidcompare/compare-tasks")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("比标任务")]
public class CompareTaskController : BidCompareController
{
    private readonly ICompareTaskAppService _appService;

    public CompareTaskController(ICompareTaskAppService appService)
    {
        _appService = appService;
    }

    /// <summary>POST /api/bidcompare/compare-tasks 创建任务（含条款清单快照）</summary>
    /// <param name="input">任务创建参数</param>
    /// <returns>创建成功的比标任务</returns>
    [HttpPost]
    public Task<CompareTaskDto> CreateAsync([FromBody] CreateCompareTaskDto input)
        => _appService.CreateAsync(input);

    /// <summary>GET /api/bidcompare/compare-tasks/{id} 任务详情 + 状态机状态 + 各阶段进度</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>任务详情</returns>
    [HttpGet("{id}")]
    public Task<CompareTaskDto> GetAsync(Guid id)
        => _appService.GetAsync(id);

    /// <summary>GET /api/bidcompare/compare-tasks/{id}/documents 任务文档列表（前端详情/历史用）</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>任务文档列表</returns>
    [HttpGet("{id}/documents")]
    public Task<List<CompareDocumentDto>> GetDocumentsAsync(Guid id)
        => _appService.GetDocumentsAsync(id);

    /// <summary>GET /api/bidcompare/compare-tasks 任务列表（分页，PagedResultDto）</summary>
    /// <param name="input">查询条件</param>
    /// <returns>分页的任务列表</returns>
    [HttpGet]
    public Task<PagedResultDto<CompareTaskDto>> GetListAsync([FromQuery] GetCompareTasksInput input)
        => _appService.GetListAsync(input);

    /// <summary>DELETE /api/bidcompare/compare-tasks/{id} 删除任务（补充路由，spec §7.1 操作列删除）</summary>
    /// <param name="id">任务 ID</param>
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id) => _appService.DeleteAsync(id);

    /// <summary>POST /api/bidcompare/compare-tasks/{id}/documents/reparse 重新解析失败文档（v2 §8.2，body.docIds 缺省为全部失败文档）</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="input">重解析参数，docIds 缺省为全部失败文档</param>
    /// <returns>重新解析后的任务</returns>
    [HttpPost("{id}/documents/reparse")]
    public Task<CompareTaskDto> ReparseDocumentsAsync(Guid id, [FromBody] CompareTasks.ReparseDocumentsInput? input)
        => _appService.ReparseAsync(id, input ?? new CompareTasks.ReparseDocumentsInput());

    /// <summary>POST /api/bidcompare/compare-tasks/{id}/compare/retry 重新对比（v2 §8.2，body.pairIds 缺省为全量；analyzing 时 409）</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="input">重对比参数，pairIds 缺省为全量</param>
    /// <returns>重新对比后的任务</returns>
    [HttpPost("{id}/compare/retry")]
    public Task<CompareTaskDto> RetryCompareAsync(Guid id, [FromBody] CompareTasks.RetryCompareInput? input)
        => _appService.RetryCompareAsync(id, input ?? new CompareTasks.RetryCompareInput());

    /// <summary>POST /api/bidcompare/compare-tasks/{id}/ai/retry 重新抽取 AI 分析（关键指标 + 条款矩阵，不重跑两两对比）</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>重新分析后的任务</returns>
    [HttpPost("{id}/ai/retry")]
    public Task<CompareTaskDto> RetryAiAnalysisAsync(Guid id)
        => _appService.RetryAiAnalysisAsync(id);

    /// <summary>PUT /api/bidcompare/compare-tasks/{id}/name 编辑项目名（v2 §3.3，置 nameEditedByUser = true）</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="input">新项目名</param>
    /// <returns>更新后的任务</returns>
    [HttpPut("{id}/name")]
    public Task<CompareTaskDto> UpdateNameAsync(Guid id, [FromBody] CompareTasks.UpdateCompareTaskNameInput input)
        => _appService.UpdateNameAsync(id, input);

    /// <summary>POST /api/bidcompare/compare-tasks/{id}/documents 上传文档（标书/招标文件，区分 role）</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="form">上传文档表单（文件 + 角色）</param>
    /// <returns>上传成功的文档</returns>
    [HttpPost("{id}/documents")]
    [RequestSizeLimit(200 * 1024 * 1024)] // 单份标书 100~500 页 PDF，放宽到 200MB
    public async Task<CompareDocumentDto> UploadDocumentAsync(Guid id, [FromForm] UploadDocumentForm form)
    {
        await using var stream = form.File.OpenReadStream();
        return await _appService.UploadDocumentAsync(id, form.Role, form.File.FileName, stream);
    }

    /// <summary>POST /api/bidcompare/compare-tasks/{id}/documents/parse 上传完成后批量并发解析（v2 修订：不再逐份入队）</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>触发解析后的任务</returns>
    [HttpPost("{id}/documents/parse")]
    public Task<CompareTaskDto> StartParsingAsync(Guid id)
        => _appService.StartParsingAsync(id);

    /// <summary>GET /api/bidcompare/compare-tasks/{id}/documents/{docId}/file 文档原文（PDF Viewer 预览用）</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="docId">文档 ID</param>
    /// <returns>文档文件流（支持 Range 请求）</returns>
    [HttpGet("{id}/documents/{docId}/file")]
    public async Task<IActionResult> GetDocumentFileAsync(Guid id, Guid docId)
    {
        var result = await _appService.GetDocumentFileAsync(id, docId);
        return new FileStreamResult(result.Content, result.ContentType)
        {
            EnableRangeProcessing = true, // pdf.js 流式加载依赖 Range 请求
        };
    }

    /// <summary>GET /api/bidcompare/compare-tasks/{id}/documents/{docId}/ir 某文档的 IR（前端对比视图画 bbox 用）</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="docId">文档 ID</param>
    /// <returns>文档 IR 数据</returns>
    [HttpGet("{id}/documents/{docId}/ir")]
    public Task<Ir.DocumentIrDto> GetDocumentIrAsync(Guid id, Guid docId)
        => _appService.GetDocumentIrAsync(id, docId);

    /// <summary>GET /api/bidcompare/compare-tasks/{id}/evidences 证据项列表（按类型/严重度/文档对过滤）</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="input">查询条件</param>
    /// <returns>分页的证据项列表</returns>
    [HttpGet("{id}/evidences")]
    public Task<PagedResultDto<Evidences.EvidenceDto>> GetEvidencesAsync(Guid id, [FromQuery] Evidences.GetEvidenceListInput input)
        => _appService.GetEvidencesAsync(id, input);

    /// <summary>GET /api/bidcompare/compare-tasks/{id}/matrix 两两相似度矩阵（N×N，热力图用）</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>相似度矩阵</returns>
    [HttpGet("{id}/matrix")]
    public Task<Analysis.SimilarityMatrixDto> GetMatrixAsync(Guid id)
        => _appService.GetMatrixAsync(id);

    /// <summary>POST /api/bidcompare/compare-tasks/{id}/clauses/extract 触发从招标文件提取条款草案（异步，草案经任务轮询返回）</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>触发提取后的任务</returns>
    [HttpPost("{id}/clauses/extract")]
    public Task<CompareTaskDto> ExtractClausesAsync(Guid id)
        => _appService.ExtractClausesAsync(id);

    /// <summary>PUT /api/bidcompare/compare-tasks/{id}/clauses 确认后的条款清单（锁定快照）</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="input">确认后的条款清单</param>
    /// <returns>更新后的任务</returns>
    [HttpPut("{id}/clauses")]
    public Task<CompareTaskDto> ConfirmClausesAsync(Guid id, [FromBody] Clauses.ConfirmClausesInput input)
        => _appService.ConfirmClausesAsync(id, input);

    /// <summary>GET /api/bidcompare/compare-tasks/{id}/report 结构化报告 JSON</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>结构化报告</returns>
    [HttpGet("{id}/report")]
    public Task<Reports.CompareReportDto> GetReportAsync(Guid id)
        => _appService.GetReportAsync(id);

    /// <summary>POST /api/bidcompare/compare-tasks/{id}/export 生成导出文件 { format } → 异步 → 下载 URL</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="input">导出请求（格式等）</param>
    /// <returns>导出任务（轮询下载 URL）</returns>
    [HttpPost("{id}/export")]
    public Task<Exports.ExportJobDto> RequestExportAsync(Guid id, [FromBody] Exports.ExportRequestDto input)
        => _appService.RequestExportAsync(id, input);

    /// <summary>GET /api/bidcompare/compare-tasks/{id}/exports/{jobId} 导出轮询（补充路由，spec §6.2）</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="jobId">导出任务 ID</param>
    /// <returns>导出任务状态</returns>
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
