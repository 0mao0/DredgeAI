namespace DredgeAI.DictManagement;

/// <summary>字典选项 DTO（用于前端下拉框）</summary>
public class DictOptionDto
{
    /// <summary>显示文本</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>选项值</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>备注说明</summary>
    public string? Remark { get; set; }

    /// <summary>排序序号</summary>
    public int Sort { get; set; }
}
