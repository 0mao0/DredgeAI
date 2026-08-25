using Volo.Abp.Auditing;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.AuditLogs;

/// <summary>实体变更 DTO</summary>
public class EntityChangeDto : EntityDto<Guid>
{
    /// <summary>所属审计日志 ID</summary>
    public Guid AuditLogId { get; set; }

    /// <summary>变更时间</summary>
    public DateTime ChangeTime { get; set; }

    /// <summary>变更类型</summary>
    public EntityChangeType ChangeType { get; set; }

    /// <summary>实体所在租户 ID</summary>
    public Guid? EntityTenantId { get; set; }

    /// <summary>实体 ID</summary>
    public string? EntityId { get; set; }

    /// <summary>实体类型完整名称</summary>
    public string? EntityTypeFullName { get; set; }

    /// <summary>属性变更列表</summary>
    public List<EntityPropertyChangeDto> PropertyChanges { get; set; } = [];
}
