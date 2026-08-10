using System;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.BidCompare.Clauses;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace DredgeAI.BidCompare.ClauseTemplates;

[RemoteService(false)]
public class ClauseTemplateAppService : ApplicationService, IClauseTemplateAppService
{
    private readonly IRepository<ClauseTemplate, Guid> _repository;

    public ClauseTemplateAppService(IRepository<ClauseTemplate, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<PagedResultDto<ClauseTemplateDto>> GetListAsync(GetClauseTemplatesInput input)
    {
        var queryable = await _repository.GetQueryableAsync();
        queryable = queryable
            .WhereIf(!input.Keyword.IsNullOrWhiteSpace(), x => x.Text.Contains(input.Keyword!))
            .WhereIf(!input.Category.IsNullOrWhiteSpace(), x => x.Category == input.Category);

        var totalCount = await AsyncExecuter.CountAsync(queryable);
        var items = await AsyncExecuter.ToListAsync(queryable
            .OrderByDescending(x => x.CreationTime)
            .PageBy(input.SkipCount, input.MaxResultCount));

        return new PagedResultDto<ClauseTemplateDto>(
            totalCount,
            items.Select(x => ObjectMapper.Map<ClauseTemplate, ClauseTemplateDto>(x)).ToList());
    }

    public async Task<ClauseTemplateDto> GetAsync(Guid id)
    {
        var entity = await _repository.GetAsync(id);
        return ObjectMapper.Map<ClauseTemplate, ClauseTemplateDto>(entity);
    }

    public async Task<ClauseTemplateDto> CreateAsync(ClauseTemplateCreateUpdateDto input)
    {
        var entity = new ClauseTemplate(GuidGenerator.Create(), input.Text.Trim(), input.Mandatory, input.Category);
        await _repository.InsertAsync(entity, autoSave: true);
        return ObjectMapper.Map<ClauseTemplate, ClauseTemplateDto>(entity);
    }

    public async Task<ClauseTemplateDto> UpdateAsync(Guid id, ClauseTemplateCreateUpdateDto input)
    {
        var entity = await _repository.GetAsync(id);
        entity.Update(input.Text.Trim(), input.Mandatory, input.Category);
        await _repository.UpdateAsync(entity, autoSave: true);
        return ObjectMapper.Map<ClauseTemplate, ClauseTemplateDto>(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id, autoSave: true);
    }
}
