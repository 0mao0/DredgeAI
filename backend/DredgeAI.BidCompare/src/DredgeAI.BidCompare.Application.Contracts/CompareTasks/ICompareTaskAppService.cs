using System;
using System.IO;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Documents;
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
}
