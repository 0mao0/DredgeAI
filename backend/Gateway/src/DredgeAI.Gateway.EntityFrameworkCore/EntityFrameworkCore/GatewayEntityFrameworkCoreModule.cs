using Microsoft.Extensions.DependencyInjection;
using Shiw.Abp.BaseEntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;

namespace DredgeAI.Gateway.EntityFrameworkCore;

[DependsOn(
    typeof(GatewayDomainModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(ShiwBaseEntityFrameworkCoreModule)
    )]
public class GatewayEntityFrameworkCoreModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<GatewayDbContext>(options =>
        {
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        Configure<AbpDbContextOptions>(options =>
        {
            /* The main point to change your DBMS.
             * See also GatewayHostDbContextFactory for EF Core tooling. */
            options.UseNpgsql();
        });
    }
}
