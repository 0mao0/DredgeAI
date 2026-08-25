using Volo.Abp.Application.Services;

namespace DredgeAI.UserPreference;

public interface IUserPreferenceAppService : IApplicationService
{
    Task<UserPreferenceDto> GetAsync();

    Task UpdateAsync(UpdateUserPreferenceDto input);
}
