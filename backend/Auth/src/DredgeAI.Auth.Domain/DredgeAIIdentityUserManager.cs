using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.Caching;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.Settings;
using Volo.Abp.Threading;
using IdentityUser = Volo.Abp.Identity.IdentityUser;

namespace DredgeAI.Controllers;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IdentityUserManager))]
public class DredgeAIIdentityUserManager : IdentityUserManager
{
    public DredgeAIIdentityUserManager(
        IdentityUserStore store,
        IIdentityRoleRepository roleRepository,
        IIdentityUserRepository userRepository,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<IdentityUser> passwordHasher,
        IEnumerable<IUserValidator<IdentityUser>> userValidators,
        IEnumerable<IPasswordValidator<IdentityUser>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<DredgeAIIdentityUserManager> logger,
        ICancellationTokenProvider cancellationTokenProvider,
        IOrganizationUnitRepository organizationUnitRepository,
        ISettingProvider settingProvider,
        IDistributedEventBus distributedEventBus,
        IIdentityLinkUserRepository identityLinkUserRepository,
        IDistributedCache<AbpDynamicClaimCacheItem> dynamicClaimCache,
        IOptions<AbpMultiTenancyOptions> multiTenancyOptions,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter) : base(
            store,
            roleRepository,
            userRepository,
            optionsAccessor,
            passwordHasher,
            userValidators,
            passwordValidators,
            keyNormalizer,
            errors,
            services,
            logger,
            cancellationTokenProvider,
            organizationUnitRepository,
            settingProvider,
            distributedEventBus,
            identityLinkUserRepository,
            dynamicClaimCache,
            multiTenancyOptions,
            currentTenant,
            dataFilter)
    {
    }

    public virtual async Task<IdentityUser?> FindByPhoneNumberAsync(string phoneNumber)
    {
        return await UserRepository.As<IMyIdentityUserRepository>().FindByPhoneNumberAsync(phoneNumber);
    }
}
