using Volo.Abp.Application.Dtos;

namespace DredgeAI.DictManagement;

/// <summary>字典类型 DTO</summary>
public class DictTypeDto : EntityDto<Guid>
{
    /// <summary>字典类型名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>字典类型编码</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>完整编码路径（含所有父级编码），格式如 "SYS:SYS_USER:SYS_USER_GENDER"</summary>
    public string FullCode { get; set; } = string.Empty;

    /// <summary>父级字典类型 ID，为 null 表示根节点</summary>
    public Guid? ParentId { get; set; }

    /// <summary>模块编码，用于分组标识</summary>
    public string? ModuleCode { get; set; }

    /// <summary>排序序号，数值越小越靠前</summary>
    public int Sort { get; set; }

    /// <summary>备注说明</summary>
    public string? Remark { get; set; }

    /// <summary>子级字典类型列表</summary>
    public List<DictTypeDto> Children { get; set; } = [];

    /// <summary>创建时间（UTC）</summary>
    public DateTime CreationTime { get; set; }

    /// <summary>是否静态数据，静态数据不允许修改和删除</summary>
    public bool IsStatic { get; set; }
}
