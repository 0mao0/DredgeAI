using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace DredgeAI.DictManagement;

/// <summary>字典数据 DTO</summary>
public class DictDataDto : EntityDto<Guid>
{
    /// <summary>所属字典类型 ID</summary>
    public Guid TypeId { get; set; }

    /// <summary>父级字典数据 ID，为 null 表示根级数据</summary>
    public Guid? ParentId { get; set; }

    /// <summary>字典数据编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>字典数据值</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>字典数据显示名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>排序序号，数值越小越靠前</summary>
    public int Sort { get; set; }

    /// <summary>是否启用</summary>
    public bool IsEnabled { get; set; }

    /// <summary>备注说明</summary>
    public string? Remark { get; set; }

    /// <summary>子级字典数据列表</summary>
    public List<DictDataDto> Children { get; set; } = [];

    /// <summary>是否静态数据，静态数据不允许修改和删除</summary>
    public bool IsStatic { get; set; }
}
