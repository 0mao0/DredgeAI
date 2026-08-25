using OpenIddict.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.OpenIddict.Controllers;

namespace DredgeAI.Controllers;

/// <summary>
/// 扩展 TokenController，支持用户名 / 邮箱 / 手机号登录。
/// 通过重写 <see cref="TokenController.ReplaceEmailToUsernameOfInputIfNeeds"/>
/// 在 ABP 默认邮箱转换基础上追加手机号转换逻辑。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(TokenController))]
public class DredgeAITokenController : TokenController
{
    protected override async Task ReplaceEmailToUsernameOfInputIfNeeds(OpenIddictRequest request)
    {
        // 1. 先走 ABP 默认逻辑：邮箱 → UserName 转换
        await base.ReplaceEmailToUsernameOfInputIfNeeds(request);

        // 2. 如果用户已存在（原始 UserName 或邮箱转换成功），无需再查手机号
        var userByUsername = await UserManager.FindSharedUserByNameAsync(request.Username);
        if (userByUsername != null)
            return;

        // 3. 纯数字输入 → 调用 UserManager.FindByPhoneNumberAsync
        if (!string.IsNullOrEmpty(request.Username) && request.Username.All(char.IsDigit))
        {
            var userByPhone = await (UserManager.As<DredgeAIIdentityUserManager>())
                .FindByPhoneNumberAsync(request.Username);

            if (userByPhone != null)
            {
                request.Username = userByPhone.UserName!;
            }
        }
    }
}