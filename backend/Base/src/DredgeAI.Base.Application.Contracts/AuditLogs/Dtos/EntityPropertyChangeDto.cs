using Volo.Abp.Application.Dtos;

namespace DredgeAI.AuditLogs;

/// <summary>实体属性变更 DTO</summary>
public class EntityPropertyChangeDto : EntityDto<Guid>
{
    /// <summary>属性名</summary>
    public string? PropertyName { get; set; }

    /// <summary>变更前值</summary>
    public string? OriginalValue { get; set; }

    /// <summary>变更后值</summary>
    public string? NewValue { get; set; }

    /// <summary>属性类型完整名称</summary>
    public string? PropertyTypeFullName { get; set; }
}
