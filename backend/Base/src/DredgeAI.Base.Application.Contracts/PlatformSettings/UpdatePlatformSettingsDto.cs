namespace DredgeAI.PlatformSettings;

/// <summary>更新平台全局设置请求</summary>
public class UpdatePlatformSettingsDto
{
    /// <summary>平台标题</summary>
    public string? PlatformTitle { get; set; }

    /// <summary>平台 Logo 完整 URL</summary>
    public string? PlatformLogoUrl { get; set; }

    /// <summary>登录页标题</summary>
    public string? LoginTitle { get; set; }

    /// <summary>登录页 Logo 完整 URL</summary>
    public string? LoginLogoUrl { get; set; }
}
