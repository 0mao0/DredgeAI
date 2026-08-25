using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace DredgeAI.DictManagement;

/// <summary>更新字典类型请求 DTO</summary>
public class UpdateDictTypeDto
{
    /// <summary>字典类型名称（必填）</summary>
    [Required]
    [DynamicStringLength(typeof(DictTypeConsts), nameof(DictTypeConsts.MaxNameLength))]
    public string Name { get; set; } = string.Empty;

    /// <summary>字典类型编码，留空表示保持不变</summary>
    [DynamicStringLength(typeof(DictTypeConsts), nameof(DictTypeConsts.MaxCodeLength))]
    public string? Code { get; set; }

    /// <summary>父级字典类型 ID，为 null 表示根节点</summary>
    public Guid? ParentId { get; set; }

    /// <summary>模块编码，用于分组标识</summary>
    [DynamicStringLength(typeof(DictTypeConsts), nameof(DictTypeConsts.MaxModuleCodeLength))]
    public string? ModuleCode { get; set; }

    /// <summary>排序序号，数值越小越靠前</summary>
    [DynamicRange(typeof(DictTypeConsts), typeof(int), nameof(DictTypeConsts.MinSort), nameof(DictTypeConsts.MaxSort))]
    public int Sort { get; set; }

    /// <summary>备注说明</summary>
    [DynamicStringLength(typeof(DictTypeConsts), nameof(DictTypeConsts.MaxRemarkLength))]
    public string? Remark { get; set; }
}
