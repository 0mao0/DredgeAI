using Volo.Abp.Settings;

namespace DredgeAI.Settings;

public class DredgeAIBaseSettingDefinitionProvider:SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        // 平台全局设置
        context.Add(
            new SettingDefinition(DredgeAIBaseSettings.Platform.PlatformTitle, "数算中心",
                isVisibleToClients: true),
            new SettingDefinition(DredgeAIBaseSettings.Platform.PlatformLogoUrl,
                isVisibleToClients: false),
            new SettingDefinition(DredgeAIBaseSettings.Platform.LoginTitle, "数算中心",
                isVisibleToClients: true),
            new SettingDefinition(DredgeAIBaseSettings.Platform.LoginLogoUrl,
                isVisibleToClients: false)
        );

        // 用户偏好设置
        context.Add(
            new SettingDefinition(DredgeAIBaseSettings.Platform.NavTheme, "dark",
                isVisibleToClients: true),
            new SettingDefinition(DredgeAIBaseSettings.Platform.PrimaryColor, "#1677ff",
                isVisibleToClients: true)
        );
    }
}