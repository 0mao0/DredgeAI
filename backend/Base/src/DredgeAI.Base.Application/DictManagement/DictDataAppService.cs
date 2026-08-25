using Volo.Abp;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DredgeAI.DictManagement;
using DredgeAI.Permissions;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;

namespace DredgeAI.DictManagement;

public class DictDataAppService : DredgeAIBaseAppService, IDictDataAppService
{
    private readonly IRepository<DictType, Guid> _dictTypeRepo;
    private readonly IRepository<DictData, Guid> _dictDataRepo;
    private readonly DictManager _dictManager;

    public DictDataAppService(
        IRepository<DictType, Guid> dictTypeRepo,
        IRepository<DictData, Guid> dictDataRepo,
        DictManager dictManager)
    {
        _dictTypeRepo = dictTypeRepo;
        _dictDataRepo = dictDataRepo;
        _dictManager = dictManager;
    }

    public async Task<DictDataDto> GetAsync(Guid id)
    {
        var entity = await _dictDataRepo.GetAsync(id);
        return ObjectMapper.Map<DictData, DictDataDto>(entity);
    }

    public async Task<PagedResultDto<DictDataDto>> GetListAsync(GetDictDataListInput input)
    {
        var dictType = await _dictManager.GetTypeByCodeAsync(input.TypeCode);

        var query = await _dictDataRepo.GetQueryableAsync();
        query = query.Where(d => d.TypeId == dictType.Id);

        if (input.ParentId.HasValue)
        {
            query = query.Where(d => d.ParentId == input.ParentId);
        }

        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            query = query.Where(d => d.Name.Contains(input.Keyword!) || d.Value.Contains(input.Keyword!));
        }

        var totalCount = await AsyncExecuter.CountAsync(query);

        var items = await AsyncExecuter.ToListAsync(
            query.OrderBy(d => d.Sort).ThenBy(d => d.Value)
                 .Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<DictDataDto>(totalCount, items.Select(e => ObjectMapper.Map<DictData, DictDataDto>(e)).ToList());
    }

    public async Task<DictDataDto> CreateAsync(CreateDictDataDto input)
    {
        await _dictManager.EnsureValueUniqueAsync(input.TypeId, input.ParentId, input.Value, null);

        var dictType = await _dictTypeRepo.GetAsync(input.TypeId);
        var code = await _dictManager.GenerateDataCodeAsync(dictType.Code);

        var entity = new DictData(
            GuidGenerator.Create(), input.TypeId, input.ParentId, code,
            input.Value, input.Name, input.Sort, input.IsEnabled, input.Remark);

        await _dictDataRepo.InsertAsync(entity);

        return ObjectMapper.Map<DictData, DictDataDto>(entity);
    }

    public async Task<DictDataDto> UpdateAsync(Guid id, UpdateDictDataDto input)
    {
        var entity = await _dictDataRepo.GetAsync(id);

        if (entity.IsStatic)
            throw new BusinessException("DredgeAIBase:DictDataStaticCannotModify");

        await _dictManager.EnsureValueUniqueAsync(entity.TypeId, input.ParentId, input.Value, id);

        entity.SetName(input.Name);
        entity.SetValue(input.Value);
        entity.SetParentId(input.ParentId);
        entity.SetSort(input.Sort);
        entity.SetEnabled(input.IsEnabled);
        entity.SetRemark(input.Remark);

        await _dictDataRepo.UpdateAsync(entity);

        return ObjectMapper.Map<DictData, DictDataDto>(entity);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _dictManager.EnsureDataNotStaticAsync(id);
        await _dictManager.EnsureDataHasNoChildrenAsync(id);
        await _dictDataRepo.DeleteAsync(id);
    }

    public async Task<List<DictOptionDto>> GetOptionsAsync(DictDataOptionInput input)
    {
        var dictType = await _dictManager.GetTypeByCodeAsync(input.TypeCode);

        var query = await _dictDataRepo.GetQueryableAsync();
        query = query.Where(d => d.TypeId == dictType.Id && d.IsEnabled);

        if (input.ExcludeValues is { Count: > 0 })
        {
            query = query.Where(d => !input.ExcludeValues.Contains(d.Value));
        }

        var items = await AsyncExecuter.ToListAsync(
            query.OrderBy(d => d.Sort).ThenBy(d => d.Value));

        return items.Select(d => new DictOptionDto
        {
            Title = d.Name,
            Value = d.Value,
            Remark = d.Remark,
            Sort = d.Sort
        }).ToList();
    }

    public async Task<List<DictDataTreeNodeDto>> GetTreeAsync(Guid typeId)
    {
        var query = await _dictDataRepo.GetQueryableAsync();
        var all = await AsyncExecuter.ToListAsync(
            query.Where(d => d.TypeId == typeId));

        var roots = all.Where(d => d.ParentId == null)
                       .OrderBy(d => d.Sort).ThenBy(d => d.Value).ToList();

        return roots.Select(r => BuildDataTreeNode(r, all)).ToList();
    }

    private DictDataTreeNodeDto BuildDataTreeNode(DictData entity, List<DictData> all)
    {
        var node = ObjectMapper.Map<DictData, DictDataTreeNodeDto>(entity);
        node.Children = all.Where(d => d.ParentId == entity.Id)
                           .OrderBy(d => d.Sort).ThenBy(d => d.Value)
                           .Select(c => BuildDataTreeNode(c, all)).ToList();
        return node;
    }
}
