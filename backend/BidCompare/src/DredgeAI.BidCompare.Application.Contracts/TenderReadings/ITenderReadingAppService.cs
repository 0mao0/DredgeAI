using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Validation;

namespace DredgeAI.BidCompare.TenderReadings;

public interface ITenderReadingAppService : IApplicationService
{
    Task<TenderReadingTaskDto> CreateAsync(CreateTenderReadingTaskDto input);

    Task<TenderReadingTaskDto> GetAsync(Guid id);

    Task<PagedResultDto<TenderReadingTaskDto>> GetListAsync(GetTenderReadingTasksInput input);

    Task<TenderReadingTaskDto> UpdateAsync(Guid id, UpdateTenderReadingTaskInput input);

    Task DeleteAsync(Guid id);

    [DisableValidation] // Stream 参数无法被验证拦截器递归校验
    Task<TenderReadingDocumentDto> UploadDocumentAsync(Guid id, string fileName, Stream content);

    Task<List<TenderReadingDocumentDto>> GetDocumentsAsync(Guid id);

    Task<TenderReadingTaskDto> StartParsingAsync(Guid id);

    Task<TenderReadingTaskDto> ReparseAsync(Guid id);

    Task<List<TenderReadingOutlineNodeDto>> GetOutlineAsync(Guid id);

    Task<TenderReadingParsedDocumentDto> GetParsedDocumentAsync(Guid id);

    Task<TenderReadingBaselineDto> GetBaselineAsync(Guid id);

    Task<List<BaselineFieldDto>> GetBaselineByCategoryAsync(Guid id, BaselineCategory category);

    Task<List<SourceRefDto>> GetSourceAsync(Guid id, Guid fieldId);

    Task<BaselineFieldDto> UpdateFieldAsync(Guid id, Guid fieldId, UpdateBaselineFieldInput input);

    /// <summary>重抽基准库（后台任务执行），返回进入抽取中的任务快照。</summary>
    Task<TenderReadingTaskDto> ReExtractAsync(Guid id, ReExtractBaselineInput input);

    Task<TenderReadingDocumentFileResult> GetDocumentFileAsync(Guid id);

    Task<TenderReadingBaselineDto> ExportBaselineAsync(Guid id);
}
