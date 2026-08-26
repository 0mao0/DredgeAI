using System;
using Microsoft.Extensions.DependencyInjection;
using Shiw.Abp.BackgroundJobs.EntityFrameworkCore;
using Shiw.Abp.BaseEntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;

namespace DredgeAI.BidCompare.EntityFrameworkCore;

[DependsOn(
    typeof(BidCompareDomainModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(ShiwBackgroundJobsEntityFrameworkCoreModule),
    typeof(ShiwBaseEntityFrameworkCoreModule)
    )]
public class BidCompareEntityFrameworkCoreModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAbpDbContext<BidCompareDbContext>(options =>
        {
                /* Remove "includeAllEntities: true" to create
                 * default repositories only for aggregate roots */
            options.AddDefaultRepositories(includeAllEntities: true);
        });

        Configure<AbpDbContextOptions>(options =>
        {
                /* The main point to change your DBMS.
                 * See also BidCompareHostDbContextFactory for EF Core tooling. */
            options.UseNpgsql();
        });

    }
}
