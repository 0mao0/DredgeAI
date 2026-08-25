using Volo.Abp.Application.Dtos;

namespace DredgeAI.DictManagement;

/// <summary>字典类型分页查询输入 DTO</summary>
public class GetDictTypeListInput : PagedAndSortedResultRequestDto
{
    /// <summary>搜索关键字，按名称模糊匹配</summary>
    public string? Keyword { get; set; }

    /// <summary>父级 ID，为 null 表示查询所有根级类型</summary>
    public Guid? ParentId { get; set; }
}
