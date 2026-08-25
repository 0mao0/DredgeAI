namespace DredgeAI.Settings;

public static class DredgeAIBaseSettings
{
    public const string GroupName = "Base";

    /// <summary>平台设置分组名</summary>
    public static class Platform
    {
        private const string Prefix = GroupName + ".Platform";

        /// <summary>平台标题</summary>
        public const string PlatformTitle = Prefix + ".PlatformTitle";

        /// <summary>平台 Logo 完整 URL</summary>
        public const string PlatformLogoUrl = Prefix + ".PlatformLogoUrl";

        /// <summary>登录页标题</summary>
        public const string LoginTitle = Prefix + ".LoginTitle";

        /// <summary>登录页 Logo 完整 URL</summary>
        public const string LoginLogoUrl = Prefix + ".LoginLogoUrl";

        /// <summary>导航主题（dark / light / realDark）</summary>
        public const string NavTheme = Prefix + ".NavTheme";

        /// <summary>主题色（hex 值，如 #1677ff）</summary>
        public const string PrimaryColor = Prefix + ".PrimaryColor";
    }
}