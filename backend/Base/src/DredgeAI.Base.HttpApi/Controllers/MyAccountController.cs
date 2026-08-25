using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;

namespace DredgeAI.Controllers;

/// <summary>
/// 账号管理控制器，替换 ABP 内置的 <see cref="AccountController"/>，
/// 处理用户注册、密码重置等账号相关操作。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(AccountController), IncludeSelf = true)]
[Route($"api/{DredgeAIBaseRemoteServiceConsts.ModuleName}/account")]
[Tags("账号管理")]
[RemoteService(false)]
public class MyAccountController : AccountController
{
    public MyAccountController(IAccountAppService accountAppService) : base(accountAppService)
    {
    }

    /// <summary>
    /// 注册新用户账号。
    /// </summary>
    /// <param name="input">包含用户名、邮箱和密码的注册信息。</param>
    /// <returns>注册成功的用户信息。</returns>
    [HttpPost]
    [Route("register")]
    public override Task<IdentityUserDto> RegisterAsync(RegisterDto input)
    {
        return AccountAppService.RegisterAsync(input);
    }

    /// <summary>
    /// 向指定邮箱发送密码重置验证码。
    /// </summary>
    /// <param name="input">包含邮箱地址和 AppName 的请求信息。</param>
    [HttpPost]
    [Route("send-password-reset-code")]
    public override Task SendPasswordResetCodeAsync(SendPasswordResetCodeDto input)
    {
        return AccountAppService.SendPasswordResetCodeAsync(input);
    }

    /// <summary>
    /// 验证密码重置 Token 是否有效。
    /// </summary>
    /// <param name="input">包含用户ID和重置Token的验证请求。</param>
    /// <returns>Token 有效返回 true，否则返回 false。</returns>
    [HttpPost]
    [Route("verify-password-reset-token")]
    public override Task<bool> VerifyPasswordResetTokenAsync(VerifyPasswordResetTokenInput input)
    {
        return AccountAppService.VerifyPasswordResetTokenAsync(input);
    }

    /// <summary>
    /// 使用验证通过的 Token 重置用户密码。
    /// </summary>
    /// <param name="input">包含用户ID、重置Token和新密码的请求信息。</param>
    [HttpPost]
    [Route("reset-password")]
    public override Task ResetPasswordAsync(ResetPasswordDto input)
    {
        return AccountAppService.ResetPasswordAsync(input);
    }
}
