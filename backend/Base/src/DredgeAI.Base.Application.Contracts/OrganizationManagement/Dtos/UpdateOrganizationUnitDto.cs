namespace DredgeAI.OrganizationManagement;

/// <summary>更新组织单位请求 DTO</summary>
public class UpdateOrganizationUnitDto
{
    /// <summary>组织名称，不传表示保持不变</summary>
    public string? DisplayName { get; set; }

    /// <summary>上级组织 ID，不传表示保持不变</summary>
    public Guid? ParentId { get; set; }
}
