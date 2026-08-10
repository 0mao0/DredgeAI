using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace DredgeAI.BidCompare.ClauseTemplates;

public interface IClauseTemplateAppService : IApplicationService
{
    Task<PagedResultDto<ClauseTemplateDto>> GetListAsync(GetClauseTemplatesInput input);

    Task<ClauseTemplateDto> GetAsync(Guid id);

    Task<ClauseTemplateDto> CreateAsync(ClauseTemplateCreateUpdateDto input);

    Task<ClauseTemplateDto> UpdateAsync(Guid id, ClauseTemplateCreateUpdateDto input);

    Task DeleteAsync(Guid id);
}
