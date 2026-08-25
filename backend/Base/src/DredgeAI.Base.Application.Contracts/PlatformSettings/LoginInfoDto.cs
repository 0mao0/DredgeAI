namespace DredgeAI.PlatformSettings;

/// <summary>登录页信息 DTO（匿名访问）</summary>
public class LoginInfoDto
{
    /// <summary>登录页标题</summary>
    public string LoginTitle { get; set; } = string.Empty;

    /// <summary>登录页 Logo 完整 URL</summary>
    public string? LoginLogoUrl { get; set; }
}
