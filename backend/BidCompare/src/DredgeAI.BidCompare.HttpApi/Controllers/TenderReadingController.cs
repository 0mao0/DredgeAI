using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using DredgeAI.BidCompare.TenderReadings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>读标任务接口</summary>
[Authorize]
[Route("api/bidcompare/tender-reading-tasks")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("读标任务")]
public class TenderReadingController : BidCompareController
{
    private readonly ITenderReadingAppService _appService;

    public TenderReadingController(ITenderReadingAppService appService)
    {
        _appService = appService;
    }

    /// <summary>POST /api/bidcompare/tender-reading-tasks 创建读标任务</summary>
    /// <param name="input">任务创建参数</param>
    /// <returns>创建成功的读标任务</returns>
    [HttpPost]
    public Task<TenderReadingTaskDto> CreateAsync([FromBody] CreateTenderReadingTaskDto input)
        => _appService.CreateAsync(input);

    /// <summary>GET /api/bidcompare/tender-reading-tasks/{id} 任务详情 + 状态 + 进度</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>任务详情</returns>
    [HttpGet("{id}")]
    public Task<TenderReadingTaskDto> GetAsync(Guid id)
        => _appService.GetAsync(id);

    /// <summary>GET /api/bidcompare/tender-reading-tasks 分页查询任务列表</summary>
    /// <param name="input">查询条件</param>
    /// <returns>分页的任务列表</returns>
    [HttpGet]
    public Task<PagedResultDto<TenderReadingTaskDto>> GetListAsync([FromQuery] GetTenderReadingTasksInput input)
        => _appService.GetListAsync(input);

    /// <summary>PUT /api/bidcompare/tender-reading-tasks/{id}/name 编辑项目名 / 编号</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="input">更新参数</param>
    /// <returns>更新后的任务</returns>
    [HttpPut("{id}/name")]
    public Task<TenderReadingTaskDto> UpdateAsync(Guid id, [FromBody] UpdateTenderReadingTaskInput input)
        => _appService.UpdateAsync(id, input);

    /// <summary>DELETE /api/bidcompare/tender-reading-tasks/{id} 删除任务</summary>
    /// <param name="id">任务 ID</param>
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id) => _appService.DeleteAsync(id);

    /// <summary>GET /api/bidcompare/tender-reading-tasks/{id}/documents 任务文档列表</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>任务文档列表</returns>
    [HttpGet("{id}/documents")]
    public Task<System.Collections.Generic.List<TenderReadingDocumentDto>> GetDocumentsAsync(Guid id)
        => _appService.GetDocumentsAsync(id);

    /// <summary>POST /api/bidcompare/tender-reading-tasks/{id}/document 上传招标文件</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="form">上传文档表单</param>
    /// <returns>上传成功的文档</returns>
    [HttpPost("{id}/document")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<TenderReadingDocumentDto> UploadDocumentAsync(Guid id, [FromForm] UploadTenderDocumentForm form)
    {
        await using var stream = form.File.OpenReadStream();
        return await _appService.UploadDocumentAsync(id, form.File.FileName, stream);
    }

    /// <summary>POST /api/bidcompare/tender-reading-tasks/{id}/parse 触发解析</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>触发解析后的任务</returns>
    [HttpPost("{id}/parse")]
    public Task<TenderReadingTaskDto> StartParsingAsync(Guid id)
        => _appService.StartParsingAsync(id);

    /// <summary>POST /api/bidcompare/tender-reading-tasks/{id}/reparse 重新解析失败文档</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>重新解析后的任务</returns>
    [HttpPost("{id}/reparse")]
    public Task<TenderReadingTaskDto> ReparseAsync(Guid id)
        => _appService.ReparseAsync(id);

    /// <summary>GET /api/bidcompare/tender-reading-tasks/{id}/outline 目录树</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>文档目录树节点列表</returns>
    [HttpGet("{id}/outline")]
    public Task<System.Collections.Generic.List<TenderReadingOutlineNodeDto>> GetOutlineAsync(Guid id)
        => _appService.GetOutlineAsync(id);

    /// <summary>GET /api/bidcompare/tender-reading-tasks/{id}/document/parsed 解析产物（Markdown + IR）</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>解析产物</returns>
    [HttpGet("{id}/document/parsed")]
    public Task<TenderReadingParsedDocumentDto> GetParsedDocumentAsync(Guid id)
        => _appService.GetParsedDocumentAsync(id);

    /// <summary>GET /api/bidcompare/tender-reading-tasks/{id}/baseline 完整基准库</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>完整基准库</returns>
    [HttpGet("{id}/baseline")]
    public Task<TenderReadingBaselineDto> GetBaselineAsync(Guid id)
        => _appService.GetBaselineAsync(id);

    /// <summary>GET /api/bidcompare/tender-reading-tasks/{id}/baseline/{category} 按类目查询字段</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="category">基准类目</param>
    /// <returns>该类目下的字段列表</returns>
    [HttpGet("{id}/baseline/{category}")]
    public Task<System.Collections.Generic.List<BaselineFieldDto>> GetBaselineByCategoryAsync(Guid id, BaselineCategory category)
        => _appService.GetBaselineByCategoryAsync(id, category);

    /// <summary>GET /api/bidcompare/tender-reading-tasks/{id}/source/{fieldId} 字段原文锚点</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="fieldId">字段 ID</param>
    /// <returns>字段原文引用列表</returns>
    [HttpGet("{id}/source/{fieldId}")]
    public Task<System.Collections.Generic.List<SourceRefDto>> GetSourceAsync(Guid id, Guid fieldId)
        => _appService.GetSourceAsync(id, fieldId);

    /// <summary>PUT /api/bidcompare/tender-reading-tasks/{id}/fields/{fieldId} 人工确认 / 修改字段</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="fieldId">字段 ID</param>
    /// <param name="input">字段更新参数</param>
    /// <returns>更新后的字段</returns>
    [HttpPut("{id}/fields/{fieldId}")]
    public Task<BaselineFieldDto> UpdateFieldAsync(Guid id, Guid fieldId, [FromBody] UpdateBaselineFieldInput input)
        => _appService.UpdateFieldAsync(id, fieldId, input);

    /// <summary>POST /api/bidcompare/tender-reading-tasks/{id}/re-extract 重新抽取指定类目（缺省全量），后台执行，返回任务快照</summary>
    /// <param name="id">任务 ID</param>
    /// <param name="input">重抽取参数，类目缺省为全量</param>
    /// <returns>任务快照</returns>
    [HttpPost("{id}/re-extract")]
    public Task<TenderReadingTaskDto> ReExtractAsync(Guid id, [FromBody] ReExtractBaselineInput? input)
        => _appService.ReExtractAsync(id, input ?? new ReExtractBaselineInput());

    /// <summary>GET /api/bidcompare/tender-reading-tasks/{id}/document/file 文档原文流（PDF 预览）</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>文档文件流（支持 Range 请求）</returns>
    [HttpGet("{id}/document/file")]
    public async Task<IActionResult> GetDocumentFileAsync(Guid id)
    {
        var result = await _appService.GetDocumentFileAsync(id);
        return new FileStreamResult(result.Content, result.ContentType)
        {
            EnableRangeProcessing = true // pdf.js 流式加载依赖 Range 请求
        };
    }

    /// <summary>GET /api/bidcompare/tender-reading-tasks/{id}/export 导出基准库 JSON</summary>
    /// <param name="id">任务 ID</param>
    /// <returns>导出的基准库数据</returns>
    [HttpGet("{id}/export")]
    public Task<TenderReadingBaselineDto> ExportBaselineAsync(Guid id)
        => _appService.ExportBaselineAsync(id);
}

public class UploadTenderDocumentForm
{
    [Required]
    public IFormFile File { get; set; } = default!;
}
