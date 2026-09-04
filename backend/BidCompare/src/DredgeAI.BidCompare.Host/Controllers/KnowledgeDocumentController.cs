using System.IO;
using System.Threading.Tasks;
using DredgeAI.BidCompare.AnGineer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

/// <summary>
/// AI 晨会知识库：上传施组方案 PDF/Word → AnGIneer 解析（stages=all），
/// 前端轮询状态，完成后即可被知识检索命中。
/// </summary>
[Route("api/meeting/knowledge/documents")]
[Authorize]
public class KnowledgeDocumentController : AbpControllerBase
{
    private readonly IAnGineerClient _anGineer;

    public KnowledgeDocumentController(IAnGineerClient anGineer)
    {
        _anGineer = anGineer;
    }

    [HttpPost]
    public async Task<KnowledgeUploadResult> Upload(IFormFile file)
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

    [HttpGet("{docId}/status")]
    public async Task<KnowledgeJobStatusDto> Status(string docId)
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
