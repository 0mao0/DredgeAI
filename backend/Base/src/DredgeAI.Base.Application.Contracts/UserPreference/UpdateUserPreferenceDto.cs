namespace DredgeAI.UserPreference;

/// <summary>更新用户偏好请求</summary>
public class UpdateUserPreferenceDto
{
    /// <summary>导航主题（dark / light / realDark）</summary>
    public string? NavTheme { get; set; }

    /// <summary>主题色（hex 值，如 #1677ff）</summary>
    public string? PrimaryColor { get; set; }
}
