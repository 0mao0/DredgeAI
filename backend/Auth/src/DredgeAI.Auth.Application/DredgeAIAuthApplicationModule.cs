using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Application;
using Volo.Abp.Mapperly;
using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(
    typeof(DredgeAIAuthDomainModule),
    typeof(DredgeAIAuthApplicationContractsModule),
    typeof(AbpDddApplicationModule),
    typeof(AbpMapperlyModule)
)]
public class DredgeAIAuthApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddMapperlyObjectMapper<DredgeAIAuthApplicationModule>();
    }
}