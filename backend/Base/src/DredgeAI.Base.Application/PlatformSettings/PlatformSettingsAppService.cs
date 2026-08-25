using DredgeAI.PlatformSettings;
using DredgeAI.Settings;
using Volo.Abp.SettingManagement;

namespace DredgeAI.PlatformSettings;

public class PlatformSettingsAppService : DredgeAIBaseAppService, IPlatformSettingsAppService
{
    private readonly ISettingManager _settingManager;

    public PlatformSettingsAppService(ISettingManager settingManager)
    {
        _settingManager = settingManager;
    }

    public async Task<PlatformSettingsDto> GetAsync()
    {
        return new PlatformSettingsDto
        {
            PlatformTitle = await _settingManager.GetOrNullGlobalAsync(DredgeAIBaseSettings.Platform.PlatformTitle)
                            ?? "数算中心",
            PlatformLogoUrl = await _settingManager.GetOrNullGlobalAsync(DredgeAIBaseSettings.Platform.PlatformLogoUrl),
            LoginTitle = await _settingManager.GetOrNullGlobalAsync(DredgeAIBaseSettings.Platform.LoginTitle)
                         ?? "数算中心",
            LoginLogoUrl = await _settingManager.GetOrNullGlobalAsync(DredgeAIBaseSettings.Platform.LoginLogoUrl),
        };
    }

    public async Task UpdateAsync(UpdatePlatformSettingsDto input)
    {
        if (input.PlatformTitle != null)
            await _settingManager.SetGlobalAsync(DredgeAIBaseSettings.Platform.PlatformTitle, input.PlatformTitle);
        if (input.PlatformLogoUrl != null)
            await _settingManager.SetGlobalAsync(DredgeAIBaseSettings.Platform.PlatformLogoUrl, input.PlatformLogoUrl);
        if (input.LoginTitle != null)
            await _settingManager.SetGlobalAsync(DredgeAIBaseSettings.Platform.LoginTitle, input.LoginTitle);
        if (input.LoginLogoUrl != null)
            await _settingManager.SetGlobalAsync(DredgeAIBaseSettings.Platform.LoginLogoUrl, input.LoginLogoUrl);
    }

    public async Task<LoginInfoDto> GetLoginInfoAsync()
    {
        return new LoginInfoDto
        {
            LoginTitle = await _settingManager.GetOrNullGlobalAsync(DredgeAIBaseSettings.Platform.LoginTitle)
                         ?? "数算中心",
            LoginLogoUrl = await _settingManager.GetOrNullGlobalAsync(DredgeAIBaseSettings.Platform.LoginLogoUrl),
        };
    }
}
