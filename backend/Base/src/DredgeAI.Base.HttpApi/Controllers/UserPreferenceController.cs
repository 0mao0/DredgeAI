using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using DredgeAI.UserPreference;
using Volo.Abp;

namespace DredgeAI.Controllers;

/// <summary>
/// 用户偏好控制器，管理当前用户的主题和颜色偏好
/// </summary>
[Authorize]
[Route("api/base/user-preferences")]
[RemoteService(Name = DredgeAIBaseRemoteServiceConsts.RemoteServiceName)]
[Area(DredgeAIBaseRemoteServiceConsts.ModuleName)]
[Tags("用户偏好")]
public class UserPreferenceController : DredgeAIBaseController,IUserPreferenceAppService
{
    private readonly IUserPreferenceAppService _appService;

    public UserPreferenceController(IUserPreferenceAppService appService)
    {
        _appService = appService;
    }

    /// <summary>获取当前用户的偏好设置</summary>
    [HttpGet]
    public Task<UserPreferenceDto> GetAsync()
    {
        return _appService.GetAsync();
    }

    /// <summary>更新当前用户的偏好设置</summary>
    [HttpPut]
    public Task UpdateAsync(UpdateUserPreferenceDto input)
    {
        return _appService.UpdateAsync(input);
    }
}
