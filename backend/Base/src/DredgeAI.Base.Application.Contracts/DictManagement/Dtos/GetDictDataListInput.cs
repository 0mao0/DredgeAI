using System.ComponentModel.DataAnnotations;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.DictManagement;

/// <summary>字典数据分页查询输入 DTO</summary>
public class GetDictDataListInput : PagedAndSortedResultRequestDto
{
    /// <summary>字典类型编码（必填）</summary>
    [Required]
    public string TypeCode { get; set; } = string.Empty;

    /// <summary>父级 ID，为 null 表示查询所有根级数据</summary>
    public Guid? ParentId { get; set; }

    /// <summary>搜索关键字，按名称或编码模糊匹配</summary>
    public string? Keyword { get; set; }
}
