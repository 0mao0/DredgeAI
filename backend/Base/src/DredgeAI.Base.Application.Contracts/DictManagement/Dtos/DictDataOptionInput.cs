using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DredgeAI.DictManagement;

/// <summary>字典数据选项查询输入 DTO</summary>
public class DictDataOptionInput
{
    /// <summary>字典类型编码（必填）</summary>
    [Required]
    public string TypeCode { get; set; } = string.Empty;

    /// <summary>需要排除的值列表，用于从选项列表中移除特定项</summary>
    public List<string>? ExcludeValues { get; set; }
}
