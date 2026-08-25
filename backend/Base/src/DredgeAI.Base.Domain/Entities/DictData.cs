using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI;

public class DictData : FullAuditedAggregateRoot<Guid>
{
    public Guid TypeId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string Code { get; private set; }
    public string Value { get; private set; }
    public string Name { get; private set; }
    public int Sort { get; private set; }
    public bool IsEnabled { get; private set; }
    public string? Remark { get; private set; }
    public bool IsStatic { get; private set; }

    protected DictData() { }

    internal DictData(Guid id, Guid typeId, Guid? parentId, string code,
        string value, string name, int sort, bool isEnabled,
        string? remark, bool isStatic = false) : base(id)
    {
        TypeId = typeId;
        ParentId = parentId;
        SetCode(code);
        SetValue(value);
        SetName(name);
        Sort = sort;
        IsEnabled = isEnabled;
        Remark = remark;
        IsStatic = isStatic;
    }

    internal void SetName(string name)
        => Name = Check.NotNullOrWhiteSpace(name, nameof(name), DictDataConsts.MaxNameLength);

    internal void SetValue(string value)
        => Value = Check.NotNullOrWhiteSpace(value, nameof(value), DictDataConsts.MaxValueLength);

    internal void SetCode(string code)
        => Code = Check.NotNullOrWhiteSpace(code, nameof(code), DictDataConsts.MaxCodeLength);

    internal void SetParentId(Guid? parentId) => ParentId = parentId;
    internal void SetSort(int sort) => Sort = sort;
    internal void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;
    internal void SetRemark(string? remark) => Remark = remark;

    internal void SetStatic(bool isStatic) => IsStatic = isStatic;
}
