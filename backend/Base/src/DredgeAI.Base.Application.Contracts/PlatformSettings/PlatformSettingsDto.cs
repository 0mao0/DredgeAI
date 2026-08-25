namespace DredgeAI.PlatformSettings;

/// <summary>平台全局设置 DTO</summary>
public class PlatformSettingsDto
{
    /// <summary>平台标题</summary>
    public string PlatformTitle { get; set; } = string.Empty;

    /// <summary>平台 Logo 完整 URL</summary>
    public string? PlatformLogoUrl { get; set; }

    /// <summary>登录页标题</summary>
    public string LoginTitle { get; set; } = string.Empty;

    /// <summary>登录页 Logo 完整 URL</summary>
    public string? LoginLogoUrl { get; set; }
}
