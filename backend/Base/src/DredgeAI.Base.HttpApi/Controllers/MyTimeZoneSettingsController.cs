using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.DependencyInjection;
using Volo.Abp.SettingManagement;

namespace DredgeAI.Controllers;

/// <summary>
/// 时区设置控制器，替换 ABP 内置的 <see cref="TimeZoneSettingsController"/>，
/// 管理系统的时区配置。
/// 基类方法非 virtual，不可重写；类级替换即可，路由由基类自动处理。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(TimeZoneSettingsController), IncludeSelf = true)]
[Route($"api/{DredgeAIBaseRemoteServiceConsts.ModuleName}/setting-management/timezone")]
[Tags("设置管理")]
public class MyTimeZoneSettingsController : TimeZoneSettingsController
{
    public MyTimeZoneSettingsController(ITimeZoneSettingsAppService timeZoneSettingsAppService) : base(timeZoneSettingsAppService)
    {
    }
}
