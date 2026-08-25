using System.Collections.Generic;

namespace DredgeAI.DictManagement;

/// <summary>字典数据树节点 DTO</summary>
public class DictDataTreeNodeDto
{
    /// <summary>字典数据 ID</summary>
    public Guid Id { get; set; }

    /// <summary>字典数据值</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>字典数据显示名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>字典数据编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>父级 ID</summary>
    public Guid? ParentId { get; set; }

    /// <summary>子级节点列表</summary>
    public List<DictDataTreeNodeDto> Children { get; set; } = [];

    /// <summary>是否静态数据，静态数据不允许修改和删除</summary>
    public bool IsStatic { get; set; }
}

