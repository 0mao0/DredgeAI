using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.AspNetCore.Mvc.ApplicationConfigurations;
using Volo.Abp.DependencyInjection;

namespace DredgeAI.Controllers;

[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(AbpApplicationConfigurationController), IncludeSelf = true)]
[Route($"api/{DredgeAIBaseRemoteServiceConsts.ModuleName}/application-configuration")]
// 静态客户端代理（Volo.Abp.AspNetCore.Mvc.Client 远程权限校验）硬编码请求默认路径，必须保留该别名
[Route("api/abp/application-configuration")]
[Tags("应用配置")]
public class MyApplicationConfigurationController : AbpApplicationConfigurationController
{
    public MyApplicationConfigurationController(
        IAbpApplicationConfigurationAppService applicationConfigurationAppService,
        IAbpAntiForgeryManager antiForgeryManager) : base(applicationConfigurationAppService, antiForgeryManager)
    {
    }
}