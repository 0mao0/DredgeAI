using System.Collections.Generic;

namespace DredgeAI.BidCompare.Applications;

/// <summary>应用权限树节点：第一层类型（Key=枚举 snake_case wire 值，Title=本地化描述）、第二层主应用、第三层子应用（Key=应用 Id 字符串，Title=名称）。</summary>
public class AppPermissionTreeNodeDto
{
    public string Key { get; set; } = default!;

    public string Title { get; set; } = default!;

    /// <summary>子节点；无子应用的主应用为 null。</summary>
    public List<AppPermissionTreeNodeDto>? Children { get; set; }
}
