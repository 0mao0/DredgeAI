namespace DredgeAI.UserManagement;

/// <summary>组织单位简要 DTO</summary>
public class OrganizationUnitBriefDto
{
    /// <summary>组织 ID</summary>
    public Guid Key { get; set; }

    /// <summary>组织名称</summary>
    public string Name { get; set; } = string.Empty;
}
