using Volo.Abp.Application.Dtos;

namespace DredgeAI.OrganizationManagement;

/// <summary>组织单位 DTO</summary>
public class OrganizationUnitDto : EntityDto<Guid>
{
    /// <summary>层级编码（ABP 自动生成，如 "0001.0002"）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>组织名称</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>上级组织 ID，null 表示根节点</summary>
    public Guid? ParentId { get; set; }

    /// <summary>创建时间（UTC）</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>子级组织列表</summary>
    public List<OrganizationUnitDto> Children { get; set; } = [];
}
