using System.ComponentModel.DataAnnotations;

namespace DredgeAI.OrganizationManagement;

/// <summary>创建组织单位请求 DTO</summary>
public class CreateOrganizationUnitDto
{
    /// <summary>组织名称（必填）</summary>
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>上级组织 ID，null 表示根节点</summary>
    public Guid? ParentId { get; set; }
}
