using Volo.Abp.Application.Dtos;

namespace DredgeAI.OrganizationManagement;

/// <summary>组织单位分页查询输入 DTO</summary>
public class GetOrganizationUnitListInput : PagedAndSortedResultRequestDto
{
    /// <summary>搜索关键字，按名称模糊匹配</summary>
    public string? Keyword { get; set; }
}
