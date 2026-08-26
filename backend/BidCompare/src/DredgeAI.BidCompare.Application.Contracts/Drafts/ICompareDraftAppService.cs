using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Documents;
using Volo.Abp.Application.Services;
using Volo.Abp.Validation;

namespace DredgeAI.BidCompare.Drafts;

/// <summary>上传会话（仅暂存文件，不建任务、不解析）。</summary>
public interface ICompareDraftAppService : IApplicationService
{
    Task<List<CompareDraftDocumentDto>> GetDocumentsAsync(Guid draftId);

    [DisableValidation] // Stream 参数无法被验证拦截器递归校验
    Task<CompareDraftDocumentDto> UploadDocumentAsync(Guid draftId, DocumentRole role, string fileName, Stream content);

    Task DeleteDocumentAsync(Guid draftId, Guid docId);

    Task DeleteDraftAsync(Guid draftId);
}
