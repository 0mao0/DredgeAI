using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using DredgeAI.Controllers;
using Volo.Abp.Account.Web;
using Volo.Abp.Account.Web.Pages.Account;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.OpenIddict;

namespace DredgeAI.Pages;

[ExposeServices(typeof(LoginModel))]
public class MyLoginModel : OpenIddictSupportedLoginModel
{
    public MyLoginModel(IAuthenticationSchemeProvider schemeProvider, IOptions<AbpAccountOptions> accountOptions,
        IOptions<IdentityOptions> identityOptions,
        IdentityDynamicClaimsPrincipalContributorCache identityDynamicClaimsPrincipalContributorCache,
        AbpOpenIddictRequestHelper openIddictRequestHelper, IWebHostEnvironment webHostEnvironment) : base(
        schemeProvider, accountOptions, identityOptions, identityDynamicClaimsPrincipalContributorCache,
        openIddictRequestHelper, webHostEnvironment)
    {
    }

    protected override async Task ReplaceEmailToUsernameOfInputIfNeeds()
    {
        await base.ReplaceEmailToUsernameOfInputIfNeeds();
        var userByUsername = await UserManager.FindSharedUserByNameAsync(LoginInput.UserNameOrEmailAddress);
        if (userByUsername != null)
            return;

        // 3. 纯数字输入 → 调用 UserManager.FindByPhoneNumberAsync
        if (!string.IsNullOrEmpty(LoginInput.UserNameOrEmailAddress) &&
            LoginInput.UserNameOrEmailAddress.All(char.IsDigit))
        {
            var userByPhone = await (UserManager.As<DredgeAIIdentityUserManager>())
                .FindByPhoneNumberAsync(LoginInput.UserNameOrEmailAddress);

            if (userByPhone != null)
            {
                LoginInput.UserNameOrEmailAddress = userByPhone.UserName!;
            }
        }
    }
}