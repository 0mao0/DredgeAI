using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI;

public class DictType : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string FullCode { get; private set; }
    public Guid? ParentId { get; private set; }
    public string? ModuleCode { get; private set; }
    public int Sort { get; private set; }
    public string? Remark { get; private set; }
    public bool IsStatic { get; private set; }

    protected DictType() { }
    internal DictType(Guid id, string name, string code, string fullCode,
        Guid? parentId, string? moduleCode, int sort, string? remark,
        bool isStatic = false) : base(id)
    {
        SetName(name);
        SetCode(code);
        SetFullCode(fullCode);
        ParentId = parentId;
        ModuleCode = moduleCode;
        Remark = remark;
        IsStatic = isStatic;
    }

    internal void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), DictTypeConsts.MaxNameLength);
    }

    internal void SetCode(string code)
    {
        Code = Check.NotNullOrWhiteSpace(code, nameof(code), DictTypeConsts.MaxCodeLength);
    }

    internal void SetFullCode(string fullCode)
    {
        FullCode = Check.NotNullOrWhiteSpace(fullCode, nameof(fullCode), DictTypeConsts.MaxFullCodeLength);
    }

    internal void SetParentId(Guid? parentId) => ParentId = parentId;
    internal void SetModuleCode(string? moduleCode) => ModuleCode = moduleCode;
    internal void SetSort(int sort) => Sort = sort;
    internal void SetRemark(string? remark) => Remark = remark;

    internal void SetStatic(bool isStatic) => IsStatic = isStatic;
}
