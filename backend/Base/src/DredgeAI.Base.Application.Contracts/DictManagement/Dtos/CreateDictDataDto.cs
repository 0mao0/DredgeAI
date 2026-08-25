using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace DredgeAI.DictManagement;

/// <summary>创建字典数据请求 DTO</summary>
public class CreateDictDataDto
{
    /// <summary>所属字典类型 ID（必填）</summary>
    [Required]
    public Guid TypeId { get; set; }

    /// <summary>父级字典数据 ID，为 null 表示根级数据</summary>
    public Guid? ParentId { get; set; }

    /// <summary>字典数据值（必填），同类型同层级内唯一</summary>
    [Required]
    [DynamicStringLength(typeof(DictDataConsts), nameof(DictDataConsts.MaxValueLength))]
    public string Value { get; set; } = string.Empty;

    /// <summary>字典数据显示名称（必填）</summary>
    [Required]
    [DynamicStringLength(typeof(DictDataConsts), nameof(DictDataConsts.MaxNameLength))]
    public string Name { get; set; } = string.Empty;

    /// <summary>排序序号，数值越小越靠前</summary>
    [DynamicRange(typeof(DictDataConsts), typeof(int), nameof(DictDataConsts.MinSort), nameof(DictDataConsts.MaxSort))]
    public int Sort { get; set; }

    /// <summary>是否启用，默认 true</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>备注说明</summary>
    [DynamicStringLength(typeof(DictDataConsts), nameof(DictDataConsts.MaxRemarkLength))]
    public string? Remark { get; set; }
}
