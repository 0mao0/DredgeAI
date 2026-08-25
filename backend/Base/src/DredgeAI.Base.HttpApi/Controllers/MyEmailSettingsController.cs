using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.DependencyInjection;
using Volo.Abp.SettingManagement;

namespace DredgeAI.Controllers;

/// <summary>
/// 邮件设置控制器，替换 ABP 内置的 <see cref="EmailSettingsController"/>，
/// 管理 SMTP 邮件服务器配置和发送测试邮件。
/// 基类方法非 virtual，不可重写；类级替换即可，路由由基类自动处理。
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(EmailSettingsController), IncludeSelf = true)]
[Route($"api/{DredgeAIBaseRemoteServiceConsts.ModuleName}/setting-management/emailing")]
[Tags("设置管理")]
public class MyEmailSettingsController : EmailSettingsController
{
    public MyEmailSettingsController(IEmailSettingsAppService emailSettingsAppService) : base(emailSettingsAppService)
    {
    }
}
