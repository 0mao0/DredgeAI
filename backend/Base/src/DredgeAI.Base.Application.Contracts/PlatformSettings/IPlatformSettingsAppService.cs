using Volo.Abp.Application.Services;

namespace DredgeAI.PlatformSettings;

public interface IPlatformSettingsAppService : IApplicationService
{
    Task<PlatformSettingsDto> GetAsync();

    Task UpdateAsync(UpdatePlatformSettingsDto input);

    Task<LoginInfoDto> GetLoginInfoAsync();
}
