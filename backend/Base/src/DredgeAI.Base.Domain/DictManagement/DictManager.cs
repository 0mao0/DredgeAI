using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.EntityFrameworkCore;

namespace DredgeAI;

public class DictManager : DomainService
{
    private readonly IRepository<DictType, Guid> _dictTypeRepo;
    private readonly IRepository<DictData, Guid> _dictDataRepo;
    private readonly DictCodeGenerator _codeGenerator;

    public DictManager(
        IRepository<DictType, Guid> dictTypeRepo,
        IRepository<DictData, Guid> dictDataRepo,
        DictCodeGenerator codeGenerator)
    {
        _dictTypeRepo = dictTypeRepo;
        _dictDataRepo = dictDataRepo;
        _codeGenerator = codeGenerator;
    }

    // ===== DictType =====

    public async Task EnsureNameUniqueAsync(string name, Guid? excludeId)
    {
        var query = await _dictTypeRepo.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(
            query.Where(t => t.Name == name).WhereIf(excludeId.HasValue, t => t.Id != excludeId!.Value));
        if (exists)
            throw new BusinessException("DredgeAIBase:DictTypeNameAlreadyExists")
                .WithData("Name", name);
    }

    public async Task EnsureCodeUniqueAsync(string code, Guid? excludeId)
    {
        var query = await _dictTypeRepo.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(
            query.Where(t => t.Code == code).WhereIf(excludeId.HasValue, t => t.Id != excludeId!.Value));
        if (exists)
            throw new BusinessException("DredgeAIBase:DictTypeCodeAlreadyExists")
                .WithData("Code", code);
    }

    public async Task<string> GenerateCodeAsync(string? moduleCode)
    {
        var prefix = string.IsNullOrWhiteSpace(moduleCode) ? "DEFAULT" : moduleCode.ToUpperInvariant();
        for (var i = 0; i < 3; i++)
        {
            var code = $"{prefix}:{_codeGenerator.GenerateRandomCode(6)}";
            var query = await _dictTypeRepo.GetQueryableAsync();
            if (!await AsyncExecuter.AnyAsync(query.Where(t => t.Code == code)))
                return code;
        }
        throw new BusinessException("DredgeAIBase:CodeGenerationFailed");
    }

    public async Task<string> GenerateFullCodeAsync(Guid? parentId)
    {
        var query = await _dictTypeRepo.GetQueryableAsync();
        var siblings = await AsyncExecuter.ToListAsync(
            query.Where(t => t.ParentId == parentId));

        var maxNumber = 0;
        foreach (var sib in siblings)
        {
            var parts = sib.FullCode.Split('.');
            if (int.TryParse(parts.Last(), out var num) && num > maxNumber)
                maxNumber = num;
        }

        var newNumber = (maxNumber + 1).ToString().PadLeft(4, '0');

        if (parentId == null)
            return newNumber;

        var parent = await _dictTypeRepo.GetAsync(parentId.Value);
        return $"{parent.FullCode}.{newNumber}";
    }

    public async Task UpdateFullCodeRecursiveAsync(DictType node)
    {
        var query = await _dictTypeRepo.GetQueryableAsync();
        var children = await AsyncExecuter.ToListAsync(
            query.Where(t => t.ParentId == node.Id));

        foreach (var child in children)
        {
            var suffix = child.FullCode.Split('.').Last();
            child.SetFullCode($"{node.FullCode}.{suffix}");
            await _dictTypeRepo.UpdateAsync(child);
            await UpdateFullCodeRecursiveAsync(child);
        }
    }

    public async Task DeleteWithCascadeAsync(Guid id, bool cascade)
    {
        var dictType = await _dictTypeRepo.GetAsync(id);
        if (dictType.IsStatic)
            throw new BusinessException("DredgeAIBase:DictTypeStaticCannotDelete");

        var query = await _dictTypeRepo.GetQueryableAsync();
        var hasChildren = await AsyncExecuter.AnyAsync(query.Where(t => t.ParentId == id));

        if (hasChildren && !cascade)
            throw new BusinessException("DredgeAIBase:DictTypeCannotDeleteWithChildren");

        if (cascade)
        {
            var children = await AsyncExecuter.ToListAsync(query.Where(t => t.ParentId == id));
            foreach (var child in children)
            {
                await DeleteWithCascadeAsync(child.Id, true);
                await _dictDataRepo.DeleteAsync(d => d.TypeId == child.Id);
                await _dictTypeRepo.DeleteAsync(child);
            }
        }

        await _dictDataRepo.DeleteAsync(d => d.TypeId == id);
        await _dictTypeRepo.DeleteAsync(id);
    }

    // ===== DictData =====

    public async Task EnsureValueUniqueAsync(Guid typeId, Guid? parentId, string value, Guid? excludeId)
    {
        var query = await _dictDataRepo.GetQueryableAsync();
        var exists = await AsyncExecuter.AnyAsync(
            query.Where(d => d.TypeId == typeId && d.ParentId == parentId && d.Value == value)
                 .WhereIf(excludeId.HasValue, d => d.Id != excludeId!.Value));
        if (exists)
            throw new BusinessException("DredgeAIBase:DictDataValueAlreadyExists")
                .WithData("Value", value);
    }

    public async Task<string> GenerateDataCodeAsync(string typeCode)
    {
        for (var i = 0; i < 3; i++)
        {
            var code = $"{typeCode}:{_codeGenerator.GenerateRandomCode(8)}";
            var query = await _dictDataRepo.GetQueryableAsync();
            if (!await AsyncExecuter.AnyAsync(query.Where(d => d.Code == code)))
                return code;
        }
        throw new BusinessException("DredgeAIBase:CodeGenerationFailed");
    }

    public async Task EnsureDataHasNoChildrenAsync(Guid id)
    {
        var query = await _dictDataRepo.GetQueryableAsync();
        if (await AsyncExecuter.AnyAsync(query.Where(d => d.ParentId == id)))
            throw new BusinessException("DredgeAIBase:DictDataCannotDeleteWithChildren");
    }

    /// <summary>
    /// 检查字典数据是否为静态数据，静态数据不允许删除。
    /// </summary>
    public async Task EnsureDataNotStaticAsync(Guid id)
    {
        var entity = await _dictDataRepo.GetAsync(id);
        if (entity.IsStatic)
            throw new BusinessException("DredgeAIBase:DictDataStaticCannotDelete");
    }


    /// <summary>
    /// 判断指定 Code 的字典类型是否已存在（用于种子数据幂等检查）。
    /// </summary>
    public async Task<bool> DictTypeExistsByCodeAsync(string code)
    {
        var query = await _dictTypeRepo.GetQueryableAsync();
        return await AsyncExecuter.AnyAsync(query.Where(t => t.Code == code));
    }

    public async Task<DictType> GetTypeByCodeAsync(string typeCode)
    {
        var query = await _dictTypeRepo.GetQueryableAsync();
        return await AsyncExecuter.FirstOrDefaultAsync(query.Where(t => t.Code == typeCode))
            ?? throw new EntityNotFoundException(typeof(DictType), typeCode);
    }

    // ===== Factory Methods =====

    /// <summary>
    /// 创建字典类型（自动生成编码与层级路径）。
    /// 该方法由种子数据或外部调用方使用，走完整校验 + 编码生成流程。
    /// </summary>
    public async Task<DictType> CreateDictTypeAsync(
        string name,
        string? code,
        Guid? parentId,
        string? moduleCode,
        int sort,
        string? remark,
        bool isStatic = false,
        bool autoSave = true)
    {
        await EnsureNameUniqueAsync(name, null);
        code ??= await GenerateCodeAsync(moduleCode);
        await EnsureCodeUniqueAsync(code, null);
        var fullCode = await GenerateFullCodeAsync(parentId);

        var entity = new DictType(
            GuidGenerator.Create(), name, code, fullCode,
            parentId, moduleCode, sort, remark, isStatic);

        return await _dictTypeRepo.InsertAsync(entity, autoSave);
    }

    /// <summary>
    /// 创建字典数据值（自动生成编码）。
    /// </summary>
    public async Task<DictData> CreateDictDataAsync(
        Guid typeId,
        Guid? parentId,
        string value,
        string name,
        int sort,
        bool isEnabled,
        string? remark,
        bool isStatic = false,
        bool autoSave = true)
    {
        await EnsureValueUniqueAsync(typeId, parentId, value, null);
        var type = await _dictTypeRepo.GetAsync(typeId);
        var code = await GenerateDataCodeAsync(type.Code);

        var entity = new DictData(
            GuidGenerator.Create(), typeId, parentId, code,
            value, name, sort, isEnabled, remark, isStatic);

        return await _dictDataRepo.InsertAsync(entity, autoSave);
    }
}
