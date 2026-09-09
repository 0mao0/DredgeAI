using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Identity.Settings;
using Volo.Abp.Settings;
using Volo.Abp.ObjectExtending;
using Volo.Abp.Users;

namespace DredgeAI.Account;

[Authorize]
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IProfileAppService))]
public class ProfileAppService : DredgeAIBaseAppService, IProfileAppService
{
    protected IdentityUserManager UserManager { get; }
    protected IIdentityUserRepository UserRepository { get; }
    protected IOptions<IdentityOptions> IdentityOptions { get; }
    protected IIdentitySecurityLogRepository SecurityLogRepository { get; }

    public ProfileAppService(
        IdentityUserManager userManager,
        IIdentityUserRepository userRepository,
        IOptions<IdentityOptions> identityOptions,
        IIdentitySecurityLogRepository securityLogRepository)
    {
        UserManager = userManager;
        UserRepository = userRepository;
        IdentityOptions = identityOptions;
        SecurityLogRepository = securityLogRepository;
    }

    public virtual async Task<ProfileDto> GetAsync()
    {
        var user = await UserManager.GetByIdAsync(CurrentUser.GetId());

        return await BuildProfileDtoAsync(user);
    }

    public virtual async Task<ProfileDto> UpdateAsync(UpdateProfileDto input)
    {
        await IdentityOptions.SetAsync();

        var user = await UserManager.GetByIdAsync(CurrentUser.GetId());

        user.SetConcurrencyStampIfNotNull(input.ConcurrencyStamp);

        if (!string.Equals(user.UserName, input.UserName, StringComparison.InvariantCultureIgnoreCase))
        {
            if (await SettingProvider.IsTrueAsync(IdentitySettingNames.User.IsUserNameUpdateEnabled))
            {
                (await UserManager.SetUserNameAsync(user, input.UserName)).CheckErrors();
            }
        }

        if (!string.Equals(user.Email, input.Email, StringComparison.InvariantCultureIgnoreCase))
        {
            if (await SettingProvider.IsTrueAsync(IdentitySettingNames.User.IsEmailUpdateEnabled))
            {
                (await UserManager.SetEmailAsync(user, input.Email)).CheckErrors();
            }
        }

        if (user.PhoneNumber.IsNullOrWhiteSpace() && input.PhoneNumber.IsNullOrWhiteSpace())
        {
            input.PhoneNumber = user.PhoneNumber;
        }

        if (!string.Equals(user.PhoneNumber, input.PhoneNumber, StringComparison.InvariantCultureIgnoreCase))
        {
            (await UserManager.SetPhoneNumberAsync(user, input.PhoneNumber)).CheckErrors();
        }

        user.Name = input.Name?.Trim();
        user.Surname = input.Surname?.Trim();

        input.MapExtraPropertiesTo(user);

        (await UserManager.UpdateAsync(user)).CheckErrors();

        await CurrentUnitOfWork.SaveChangesAsync();

        return await BuildProfileDtoAsync(user);
    }

    public virtual async Task ChangePasswordAsync(ChangePasswordInput input)
    {
        await IdentityOptions.SetAsync();

        var currentUser = await UserManager.GetByIdAsync(CurrentUser.GetId());

        if (currentUser.IsExternal)
        {
            throw new BusinessException(code: IdentityErrorCodes.ExternalUserPasswordChange);
        }

        if (currentUser.PasswordHash == null)
        {
            (await UserManager.AddPasswordAsync(currentUser, input.NewPassword)).CheckErrors();

            return;
        }

        (await UserManager.ChangePasswordAsync(currentUser, input.CurrentPassword, input.NewPassword)).CheckErrors();
    }

    protected virtual async Task<ProfileDto> BuildProfileDtoAsync(IdentityUser user)
    {
        var dto = ObjectMapper.Map<IdentityUser, ProfileDto>(user);
        dto.HasPassword = user.PasswordHash != null;

        var roles = await UserManager.GetRolesAsync(user);
        dto.SetProperty(UserExtensionConsts.RoleNamesPropertyName, roles.ToArray());

        var organizationUnits = await UserRepository.GetOrganizationUnitsAsync(user.Id);
        dto.SetProperty(
            UserExtensionConsts.DepartmentNamesPropertyName,
            organizationUnits.Select(o => o.DisplayName).ToArray());

        dto.SetProperty(UserExtensionConsts.CreationTimePropertyName, user.CreationTime);
        dto.SetProperty(UserExtensionConsts.LastLoginTimePropertyName, await GetLastLoginTimeAsync(user.Id));

        return dto;
    }

    protected virtual async Task<DateTime?> GetLastLoginTimeAsync(Guid userId)
    {
        var logs = await SecurityLogRepository.GetListAsync(
            sorting: "CreationTime desc",
            maxResultCount: 1,
            action: IdentitySecurityLogActionConsts.LoginSucceeded,
            userId: userId,
            includeDetails: false);

        return logs.FirstOrDefault()?.CreationTime;
    }
}
