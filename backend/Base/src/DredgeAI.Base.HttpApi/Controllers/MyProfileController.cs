using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Account;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.Controllers;

/// <summary>
/// 个人资料控制器，替换 ABP 内置的 <see cref="ProfileController"/>，
/// 处理当前用户的个人信息查看、修改和密码变更。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(ProfileController), IncludeSelf = true)]
[Route($"/api/{DredgeAIBaseRemoteServiceConsts.ModuleName}/account/my-profile")]
[Tags("账号管理")]
public class MyProfileController : ProfileController
{
    public MyProfileController(IProfileAppService profileAppService) : base(profileAppService)
    {
    }

    /// <summary>
    /// 获取当前登录用户的个人资料信息。
    /// </summary>
    /// <returns>包含用户名、邮箱、手机号等信息的 <see cref="ProfileDto"/>。</returns>
    [HttpGet]
    public override Task<ProfileDto> GetAsync()
    {
        return ProfileAppService.GetAsync();
    }

    /// <summary>
    /// 更新当前登录用户的个人资料（用户名、邮箱、手机号）。
    /// </summary>
    /// <param name="input">包含可更新字段的个人资料 DTO。</param>
    /// <returns>更新后的个人资料信息。</returns>
    [HttpPut]
    public override Task<ProfileDto> UpdateAsync(UpdateProfileDto input)
    {
        return ProfileAppService.UpdateAsync(input);
    }

    /// <summary>
    /// 修改当前登录用户的密码，需提供当前密码进行验证。
    /// </summary>
    /// <param name="input">包含当前密码和新密码的输入对象。</param>
    [HttpPost]
    [Route("change-password")]
    public override Task ChangePasswordAsync(ChangePasswordInput input)
    {
        return ProfileAppService.ChangePasswordAsync(input);
    }
}
