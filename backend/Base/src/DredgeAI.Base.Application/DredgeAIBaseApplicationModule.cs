using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(
    typeof(DredgeAIBaseDomainModule),
    typeof(DredgeAIBaseApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule)
)]
public class DredgeAIBaseApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<DredgeAIBaseApplicationModule>();
    }
}