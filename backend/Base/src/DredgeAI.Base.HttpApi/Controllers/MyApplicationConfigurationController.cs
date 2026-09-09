using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.Controllers;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(AbpApplicationConfigurationController), IncludeSelf = true)]
[Route($"api/{DredgeAIBaseRemoteServiceConsts.ModuleName}/application-configuration")]
[Tags("应用配置")]
public class MyApplicationConfigurationController : AbpApplicationConfigurationController
{
    public MyApplicationConfigurationController(
        IAbpApplicationConfigurationAppService applicationConfigurationAppService,
        IAbpAntiForgeryManager antiForgeryManager) : base(applicationConfigurationAppService, antiForgeryManager)
    {
    }
}