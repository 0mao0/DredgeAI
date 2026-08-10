using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Ir;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Validation;

namespace DredgeAI.BidCompare.CompareTasks;

public interface ICompareTaskAppService : IApplicationService
{
    Task<CompareTaskDto> CreateAsync(CreateCompareTaskDto input);

    Task<CompareTaskDto> GetAsync(Guid id);

    Task<PagedResultDto<CompareTaskDto>> GetListAsync(GetCompareTasksInput input);

    Task DeleteAsync(Guid id);

    [DisableValidation] // Stream 参数无法被验证拦截器递归校验（ReadTimeout 等属性不可读）
    Task<CompareDocumentDto> UploadDocumentAsync(Guid id, DocumentRole role, string fileName, Stream content);

    Task<DocumentIrDto> GetDocumentIrAsync(Guid id, Guid docId);

    Task<PagedResultDto<EvidenceDto>> GetEvidencesAsync(Guid id, GetEvidenceListInput input);

    Task<SimilarityMatrixDto> GetMatrixAsync(Guid id);

    Task<List<ClauseDto>> ExtractClausesAsync(Guid id);

    Task<CompareTaskDto> ConfirmClausesAsync(Guid id, ConfirmClausesInput input);
}
