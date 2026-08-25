using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Http.Client;
using Volo.Abp.Modularity;
using Volo.Abp.VirtualFileSystem;

namespace DredgeAI;

[DependsOn(
    typeof(DredgeAIBaseApplicationContractsModule),
    typeof(AbpHttpClientModule))]
public class DredgeAIBaseHttpApiClientModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddHttpClientProxies(
            typeof(DredgeAIBaseApplicationContractsModule).Assembly,
            DredgeAIBaseRemoteServiceConsts.RemoteServiceName
        );

        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<DredgeAIBaseHttpApiClientModule>();
        });
    }
}