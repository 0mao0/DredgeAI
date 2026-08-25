using System.ComponentModel;

namespace DredgeAI;

public enum MenuType
{
    [Description("目录")]
    Directory = 0,

    [Description("菜单")]
    Menu = 1,

    [Description("按钮")]
    Button = 2
}
