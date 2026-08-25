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

public class DictTypeAppService : DredgeAIBaseAppService, IDictTypeAppService
{
    private readonly IRepository<DictType, Guid> _dictTypeRepo;
    private readonly IRepository<DictData, Guid> _dictDataRepo;
    private readonly DictManager _dictManager;

    public DictTypeAppService(
        IRepository<DictType, Guid> dictTypeRepo,
        IRepository<DictData, Guid> dictDataRepo,
        DictManager dictManager)
    {
        _dictTypeRepo = dictTypeRepo;
        _dictDataRepo = dictDataRepo;
        _dictManager = dictManager;
    }

    public async Task<DictTypeDto> GetAsync(Guid id)
    {
        var entity = await _dictTypeRepo.GetAsync(id);
        return ObjectMapper.Map<DictType, DictTypeDto>(entity);
    }

    public async Task<PagedResultDto<DictTypeDto>> GetListAsync(GetDictTypeListInput input)
    {
        var query = await _dictTypeRepo.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Keyword))
        {
            query = query.Where(t => t.Name.Contains(input.Keyword!) || t.Code.Contains(input.Keyword!));
        }

        if (input.ParentId.HasValue)
        {
            query = query.Where(t => t.ParentId == input.ParentId);
        }

        var totalCount = await AsyncExecuter.CountAsync(query);

        var items = await AsyncExecuter.ToListAsync(
            query.OrderBy(e => e.Sort).ThenBy(e => e.Name)
                 .Skip(input.SkipCount).Take(input.MaxResultCount));

        return new PagedResultDto<DictTypeDto>(totalCount, items.Select(e => ObjectMapper.Map<DictType, DictTypeDto>(e)).ToList());
    }

    public async Task<DictTypeDto> CreateAsync(CreateDictTypeDto input)
    {
        await _dictManager.EnsureNameUniqueAsync(input.Name, null);

        var code = input.Code ?? await _dictManager.GenerateCodeAsync(input.ModuleCode);
        await _dictManager.EnsureCodeUniqueAsync(code, null);

        var fullCode = await _dictManager.GenerateFullCodeAsync(input.ParentId);

        var entity = new DictType(
            GuidGenerator.Create(), input.Name, code, fullCode,
            input.ParentId, input.ModuleCode, input.Sort, input.Remark);

        await _dictTypeRepo.InsertAsync(entity);

        return ObjectMapper.Map<DictType, DictTypeDto>(entity);
    }

    public async Task<DictTypeDto> UpdateAsync(Guid id, UpdateDictTypeDto input)
    {
        var entity = await _dictTypeRepo.GetAsync(id);

        if (entity.IsStatic)
            throw new BusinessException("DredgeAIBase:DictTypeStaticCannotModify");

        await _dictManager.EnsureNameUniqueAsync(input.Name, id);

        var code = input.Code ?? entity.Code;
        if (code != entity.Code)
        {
            await _dictManager.EnsureCodeUniqueAsync(code, id);
        }

        var parentChanged = entity.ParentId != input.ParentId;

        entity.SetName(input.Name);
        entity.SetCode(code);
        entity.SetModuleCode(input.ModuleCode);
        entity.SetSort(input.Sort);
        entity.SetRemark(input.Remark);

        if (parentChanged)
        {
            var newFullCode = await _dictManager.GenerateFullCodeAsync(input.ParentId);
            entity.SetParentId(input.ParentId);
            entity.SetFullCode(newFullCode);
            await _dictTypeRepo.UpdateAsync(entity);
            await _dictManager.UpdateFullCodeRecursiveAsync(entity);
        }
        else
        {
            await _dictTypeRepo.UpdateAsync(entity);
        }

        return ObjectMapper.Map<DictType, DictTypeDto>(entity);
    }

    public async Task DeleteAsync(Guid id, bool cascade)
    {
        await _dictManager.DeleteWithCascadeAsync(id, cascade);
    }

    public async Task<List<DictTypeTreeNodeDto>> GetTreeAsync()
    {
        var query = await _dictTypeRepo.GetQueryableAsync();
        var all = await AsyncExecuter.ToListAsync(query);

        var roots = all.Where(t => t.ParentId == null)
                       .OrderBy(t => t.Sort).ThenBy(t => t.Name).ToList();

        return roots.Select(r => BuildTypeTreeNode(r, all)).ToList();
    }

    public async Task<List<DictTypeDto>> GetChildrenAsync(Guid parentId)
    {
        var query = await _dictTypeRepo.GetQueryableAsync();

        var items = await AsyncExecuter.ToListAsync(
            query.Where(t => t.ParentId == parentId)
                 .OrderBy(t => t.Sort).ThenBy(t => t.Name));

        return items.Select(e => ObjectMapper.Map<DictType, DictTypeDto>(e)).ToList();
    }

    public async Task<string> GenerateCodeAsync(string? moduleCode)
    {
        return await _dictManager.GenerateCodeAsync(moduleCode);
    }

    private DictTypeTreeNodeDto BuildTypeTreeNode(DictType entity, List<DictType> all)
    {
        var node = ObjectMapper.Map<DictType, DictTypeTreeNodeDto>(entity);
        node.Children = all.Where(t => t.ParentId == entity.Id)
                           .OrderBy(t => t.Sort).ThenBy(t => t.Name)
                           .Select(c => BuildTypeTreeNode(c, all)).ToList();
        return node;
    }
}
