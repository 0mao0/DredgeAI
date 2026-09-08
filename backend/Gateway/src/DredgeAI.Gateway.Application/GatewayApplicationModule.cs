using DredgeAI.Gateway.Proxying;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.AutoMapper;
using Volo.Abp.Modularity;
using Yarp.ReverseProxy.Configuration;

namespace DredgeAI.Gateway;

[DependsOn(
    typeof(GatewayDomainModule),
    typeof(GatewayApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpAutoMapperModule)
    )]
public class GatewayApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<GatewayApplicationModule>();
        });

        context.Services.AddSingleton<DatabaseProxyConfigProvider>();
        context.Services.AddSingleton<IProxyConfigProvider>(sp => sp.GetRequiredService<DatabaseProxyConfigProvider>());
    }
}
