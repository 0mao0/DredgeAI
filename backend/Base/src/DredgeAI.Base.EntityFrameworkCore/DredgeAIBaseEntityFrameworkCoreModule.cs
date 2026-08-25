using Microsoft.Extensions.DependencyInjection;
using Shiw.File.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;

namespace DredgeAI;

[DependsOn(
    typeof(DredgeAIBaseDomainModule),
    typeof(AbpEntityFrameworkCoreModule),
    typeof(FileEntityFrameworkCoreModule)
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