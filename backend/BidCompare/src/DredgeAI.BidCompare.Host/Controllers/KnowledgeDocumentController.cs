using System.IO;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AnGineer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>知识库文档接口</summary>
/// <remarks>AI 晨会知识库：上传施组方案 PDF/Word → AnGIneer 解析（stages=all），
/// 前端轮询状态，完成后即可被知识检索命中。</remarks>
[Authorize]
[Route("api/bidcompare/knowledge-documents")]
[RemoteService(Name = BidCompareRemoteServiceConsts.RemoteServiceName)]
[Area(BidCompareRemoteServiceConsts.ModuleName)]
[Tags("知识库文档")]
public class KnowledgeDocumentController : BidCompareController
{
    private readonly IAnGineerClient _anGineer;

    public KnowledgeDocumentController(IAnGineerClient anGineer)
    {
        _anGineer = anGineer;
    }

    /// <summary>上传知识库文档并触发 AnGIneer 解析</summary>
    /// <param name="file">待上传的施组方案 PDF/Word 文件</param>
    /// <returns>解析任务 ID 与当前状态</returns>
    [HttpPost]
    public async Task<KnowledgeUploadResult> UploadAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();
        var docId = await _anGineer.SubmitAsync(file.FileName, () => Task.FromResult<Stream>(new MemoryStream(bytes)));
        var status = await _anGineer.GetStateAsync(docId);
        return new KnowledgeUploadResult
        {
            DocId = docId,
            Status = Map(status)
        };
    }

    /// <summary>查询知识库文档解析状态</summary>
    /// <param name="docId">AnGIneer 文档 ID</param>
    /// <returns>解析状态、进度与阶段信息</returns>
    [HttpGet("{docId}/status")]
    public async Task<KnowledgeJobStatusDto> StatusAsync(string docId)
    {
        return Map(await _anGineer.GetStateAsync(docId));
    }

    private static KnowledgeJobStatusDto Map(AnGineerJobStatus status) => new()
    {
        State = status.State switch
        {
            AnGineerJobState.Succeeded => "succeeded",
            AnGineerJobState.Failed => "failed",
            AnGineerJobState.Partial => "partial",
            _ => "processing"
        },
        Progress = status.Progress,
        Stage = status.Stage,
        StageMessage = status.FailureReason
    };

    public class KnowledgeUploadResult
    {
        public string DocId { get; set; } = "";

        public KnowledgeJobStatusDto? Status { get; set; }
    }

    public class KnowledgeJobStatusDto
    {
        public string State { get; set; } = "processing";

        public int Progress { get; set; }

        public string? Stage { get; set; }

        public string? StageMessage { get; set; }
    }
}
