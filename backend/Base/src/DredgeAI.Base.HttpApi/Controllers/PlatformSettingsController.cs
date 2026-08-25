using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DredgeAI.PlatformSettings;
using Volo.Abp;

namespace DredgeAI.Controllers;

/// <summary>
/// 平台设置控制器，管理平台全局配置（标题、Logo）
/// </summary>
[Authorize]
[Route("api/base/platform-settings")]
[RemoteService(Name = DredgeAIBaseRemoteServiceConsts.RemoteServiceName)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("平台设置")]
public class PlatformSettingsController : DredgeAIBaseController,IPlatformSettingsAppService
{
    private readonly IPlatformSettingsAppService _appService;

    public PlatformSettingsController(IPlatformSettingsAppService appService)
    {
        _appService = appService;
    }

    /// <summary>获取平台全局设置</summary>
    [HttpGet]
    public Task<PlatformSettingsDto> GetAsync()
    {
        return _appService.GetAsync();
    }

    /// <summary>更新平台全局设置</summary>
    [HttpPut]
    public Task UpdateAsync(UpdatePlatformSettingsDto input)
    {
        return _appService.UpdateAsync(input);
    }

    /// <summary>获取登录页信息（匿名访问）</summary>
    [HttpGet("login-info")]
    [AllowAnonymous]
    public Task<LoginInfoDto> GetLoginInfoAsync()
    {
        return _appService.GetLoginInfoAsync();
    }
}
