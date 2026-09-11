using System;

namespace DredgeAI.BidCompare.Applications;

public class SetAppStatusInput
{
    public Guid AppId { get; set; }

    /// <summary>目标状态：仅 online/offline。</summary>
    public AppCatalogStatus Status { get; set; }
}

public class SetSubStatusInput
{
    public Guid SubId { get; set; }

    /// <summary>目标状态：仅 published/unpublished。</summary>
    public AppCatalogStatus Status { get; set; }
}

public class SetAppFieldInput
{
    public Guid AppId { get; set; }

    public AppCatalogCategory Category { get; set; }
}

public class SetSubFieldInput
{
    public Guid SubId { get; set; }

    public AppCatalogCategory Category { get; set; }
}

public class SetAppIconInput
{
    public Guid AppId { get; set; }

    public string Icon { get; set; } = string.Empty;
}

public class SetSubIconInput
{
    public Guid SubId { get; set; }

    public string Icon { get; set; } = string.Empty;
}

public class MoveAppOrderInput
{
    public Guid AppId { get; set; }

    /// <summary>移动方向：up / down。</summary>
    public string Direction { get; set; } = string.Empty;
}

public class MoveSubAppOrderInput
{
    public Guid SubId { get; set; }

    /// <summary>移动方向：up / down。</summary>
    public string Direction { get; set; } = string.Empty;
}
