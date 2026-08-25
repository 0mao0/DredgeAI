using System.ComponentModel;

namespace DredgeAI;

public enum RouteType
{
    [Description("默认")]
    Default = 0,

    [Description("内嵌iframe")]
    IframeUrl = 1,

    [Description("新窗口")]
    OpenWindow = 2
}
