namespace DredgeAI.UserPreference;

/// <summary>用户偏好设置 DTO</summary>
public class UserPreferenceDto
{
    /// <summary>导航主题（dark / light / realDark）</summary>
    public string NavTheme { get; set; } = string.Empty;

    /// <summary>主题色（hex 值，如 #1677ff）</summary>
    public string PrimaryColor { get; set; } = string.Empty;
}
