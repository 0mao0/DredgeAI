using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Shiw.Abp.BaseEntityFrameworkCore;
using Shiw.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Uow;

namespace DredgeAI;

[DependsOn(
    typeof(DredgeAIAuthTestBaseModule),
    typeof(DredgeAIAuthEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class DredgeAIAuthEntityFrameworkCoreTestModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<AbpSqliteOptions>(x => x.BusyTimeout = null);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddTransient<IShiwDbContextHandler, DefaultShiwDbContextHandler>();

        AbpCommonDbProperties.DbTablePrefix = "tab";
        AbpIdentityDbProperties.DbTablePrefix="tab_identity";
        AbpOpenIddictDbProperties.DbTablePrefix = "tab_openid_dict";

        var sqliteConnection = new SqliteConnection("Data Source=:memory:");
        sqliteConnection.Open();

        // 引入模块内部的数据库上下文
        new ShiwIdentityDbContext(
            new DbContextOptionsBuilder<ShiwIdentityDbContext>().UseSqlite(sqliteConnection).Options,
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
