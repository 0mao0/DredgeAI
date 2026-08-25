using DredgeAI.Settings;
using DredgeAI.UserPreference;
using Volo.Abp.SettingManagement;

namespace DredgeAI.UserPreference;

public class UserPreferenceAppService : DredgeAIBaseAppService, IUserPreferenceAppService
{
    private readonly ISettingManager _settingManager;

    public UserPreferenceAppService(ISettingManager settingManager)
    {
        _settingManager = settingManager;
    }

    public async Task<UserPreferenceDto> GetAsync()
    {
        return new UserPreferenceDto
        {
            NavTheme = await _settingManager.GetOrNullForCurrentUserAsync(DredgeAIBaseSettings.Platform.NavTheme)
                       ?? "dark",
            PrimaryColor = await _settingManager.GetOrNullForCurrentUserAsync(DredgeAIBaseSettings.Platform.PrimaryColor)
                           ?? "#1677ff",
        };
    }

    public async Task UpdateAsync(UpdateUserPreferenceDto input)
    {
        if (input.NavTheme != null)
            await _settingManager.SetForCurrentUserAsync(DredgeAIBaseSettings.Platform.NavTheme, input.NavTheme);
        if (input.PrimaryColor != null)
            await _settingManager.SetForCurrentUserAsync(DredgeAIBaseSettings.Platform.PrimaryColor, input.PrimaryColor);
    }
}
