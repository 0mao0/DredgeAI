using System.Collections.Generic;

namespace DredgeAI.BidCompare.Applications;

public class UserApplicationOrderResult
{
    /// <summary>当前用户的个性化顺序（route 列表）；null = 未个性化。</summary>
    public List<string>? RouteIds { get; set; }
}

public class SetUserApplicationOrderInput
{
    public List<string>? RouteIds { get; set; }
}

public class ResetUserOrdersResult
{
    public int Count { get; set; }
}
