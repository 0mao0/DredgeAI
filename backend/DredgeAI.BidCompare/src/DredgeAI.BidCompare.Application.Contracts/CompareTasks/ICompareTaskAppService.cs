using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.Clauses;
using DredgeAI.BidCompare.Documents;
using DredgeAI.BidCompare.Evidences;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.Ir;
using DredgeAI.BidCompare.Reports;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Validation;

namespace DredgeAI.BidCompare.CompareTasks;

public interface ICompareTaskAppService : IApplicationService
{
    Task<CompareTaskDto> CreateAsync(CreateCompareTaskDto input);

    Task<CompareTaskDto> GetAsync(Guid id);

    Task<List<CompareDocumentDto>> GetDocumentsAsync(Guid id);

    Task<PagedResultDto<CompareTaskDto>> GetListAsync(GetCompareTasksInput input);

    Task DeleteAsync(Guid id);

    Task<CompareTaskDto> ReparseAsync(Guid id, ReparseDocumentsInput? input);

    Task<CompareTaskDto> RetryCompareAsync(Guid id, RetryCompareInput? input);

    Task<CompareTaskDto> UpdateNameAsync(Guid id, UpdateCompareTaskNameInput input);

    [DisableValidation] // Stream 参数无法被验证拦截器递归校验（ReadTimeout 等属性不可读）
    Task<CompareDocumentDto> UploadDocumentAsync(Guid id, DocumentRole role, string fileName, Stream content);

    /// <summary>上传完成后批量并发解析所有待解析文档（v2 修订：不再逐份入队）。</summary>
    Task<CompareTaskDto> StartParsingAsync(Guid id);

    Task<CompareDocumentFileResult> GetDocumentFileAsync(Guid id, Guid docId);

    Task<DocumentIrDto> GetDocumentIrAsync(Guid id, Guid docId);

    Task<PagedResultDto<EvidenceDto>> GetEvidencesAsync(Guid id, GetEvidenceListInput input);

    Task<SimilarityMatrixDto> GetMatrixAsync(Guid id);

    Task<List<ClauseDto>> ExtractClausesAsync(Guid id);

    Task<CompareTaskDto> ConfirmClausesAsync(Guid id, ConfirmClausesInput input);

    Task<CompareReportDto> GetReportAsync(Guid id);

    Task<ExportJobDto> RequestExportAsync(Guid id, ExportRequestDto input);

    Task<ExportJobDto> GetExportJobAsync(Guid id, Guid jobId);
}
