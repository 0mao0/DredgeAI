using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace DredgeAI;

[DependsOn(
    typeof(DredgeAIAuthApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class DredgeAIAuthHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(DredgeAIAuthApplicationContractsModule).Assembly,
            DredgeAIAuthRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DredgeAIAuthHttpApiClientModule>();
        });
    }
}