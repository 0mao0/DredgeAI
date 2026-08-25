using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(DredgeAIAuthDomainSharedModule)
)]
public class DredgeAIAuthTestBaseModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysAllowAuthorization();
    }
}
