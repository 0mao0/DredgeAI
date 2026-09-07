using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(
    typeof(DredgeAIBaseDomainModule),
    typeof(AbpEntityFrameworkCoreModule)
)]
public class DredgeAIBaseEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<DredgeAIBaseDbContext>(options =>
        {
            options.AddDefaultRepositories<IDredgeAIBaseDbContext>();
        });
    }
}