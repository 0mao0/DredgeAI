namespace DredgeAI.Common;

/// <summary>
/// Ant Design Vue 树组件（a-tree）节点数据传输对象。
/// 用于构建树形结构数据，支持 Key/Title/Icon/可选中/叶子状态等属性，
/// 也支持自定义插槽（Slots/ScopedSlots）和附加数据（Tag）。
/// </summary>
public sealed class AndtTreeDto
{
    /// <summary>
    /// 节点唯一标识键。
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// 父节点 Key。用于构建父子层级关系。
    /// </summary>
    public string ParentKey { get; set; } = string.Empty;

    /// <summary>
    /// 节点显示标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 节点类型。可为空字节值，用于区分不同种类的节点（如目录/文件/设备等）。
    /// </summary>
    public byte? Type { get; set; }

    /// <summary>
    /// 节点图标标识。
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// 是否叶子节点。默认 true；调用 <see cref="AddChildren"/> 后自动设为 false。
    /// </summary>
    public bool IsLeaf { get; set; } = true;

    /// <summary>
    /// 节点是否可被选中。默认 true。
    /// </summary>
    public bool Selectable { get; set; } = true;

    /// <summary>
    /// 自定义 CSS 类名。
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// 插槽配置（Ant Design Vue 具名插槽映射）。
    /// </summary>
    public object? Slots { get; set; }

    /// <summary>
    /// 作用域插槽配置。
    /// </summary>
    public object? ScopedSlots { get; set; }

    /// <summary>
    /// 子节点列表。
    /// </summary>
    public List<AndtTreeDto>? Children { get; set; }

    /// <summary>
    /// 添加子节点。
    /// </summary>
    /// <param name="subTree">要添加的子节点。</param>
    /// <returns>当前节点实例（支持链式调用）。</returns>
    public AndtTreeDto AddChildren(AndtTreeDto subTree)
    {
        Children ??= new List<AndtTreeDto>();
        Children.Add(subTree);
        IsLeaf = false;
        return this;
    }

    /// <summary>
    /// 附加的任意数据（如业务实体、额外属性等）。
    /// </summary>
    public object? Tag { get; set; }
}
