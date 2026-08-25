using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Shiw.Abp.BaseEntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace DredgeAI;

[DependsOn(
    typeof(DredgeAIBaseTestBaseModule),
    typeof(DredgeAIBaseEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class DredgeAIBaseEntityFrameworkCoreTestModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<AbpSqliteOptions>(x => x.BusyTimeout = null);
    }
    
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<IShiwDbContextHandler, DefaultShiwDbContextHandler>();

        var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        sqliteConnection.Open();

        //创建表
        new DredgeAIBaseDbContext(
            new DbContextOptionsBuilder<DredgeAIBaseDbContext>().UseSqlite(sqliteConnection).Options,
            new DefaultShiwDbContextHandler()
        ).GetService<IRelationalDatabaseCreator>().CreateTables();

        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(abpDbContextConfigurationContext =>
            {
                abpDbContextConfigurationContext.DbContextOptions.UseSqlite(sqliteConnection);
            });
        });
        context.Services.AddAlwaysDisableUnitOfWorkTransaction();
    }
}
