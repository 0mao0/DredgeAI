using Volo.Abp.Application.Dtos;

namespace DredgeAI.UserManagement;

/// <summary>用户分页查询输入 DTO</summary>
public class GetUserListInput : PagedAndSortedResultRequestDto
{
    /// <summary>搜索关键字，按用户名或姓名模糊匹配</summary>
    public string? Keyword { get; set; }

    /// <summary>按组织 ID 筛选</summary>
    public Guid? OrganizationUnitId { get; set; }

    /// <summary>按启用状态筛选</summary>
    public bool? IsActive { get; set; }

    /// <summary>按角色 ID 筛选</summary>
    public Guid? RoleId { get; set; }

    /// <summary>按角色名称筛选</summary>
    public string? RoleName { get; set; }
}
