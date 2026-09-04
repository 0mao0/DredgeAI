using DredgeAI.Gateway.EntityFrameworkCore;
using Shiw.Abp.BaseEntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace DredgeAI.Gateway;

[DependsOn(
    typeof(GatewayApplicationModule),
    typeof(GatewayEntityFrameworkCoreModule),
    typeof(GatewayDomainTestModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class GatewayApplicationTestModule : AbpModule
{
    private SqliteConnection? _sqliteConnection;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysDisableUnitOfWorkTransaction();

        ConfigureInMemorySqlite(context.Services);
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        _sqliteConnection?.Dispose();
    }

    private void ConfigureInMemorySqlite(IServiceCollection services)
    {
        _sqliteConnection = CreateDatabaseAndGetConnection();

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(context =>
            {
                context.DbContextOptions.UseSqlite(_sqliteConnection);
            });
        });
    }

    private static SqliteConnection CreateDatabaseAndGetConnection()
    {
        var connection = new AbpUnitTestSqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new GatewayDbContext(options, new DefaultShiwDbContextHandler()))
        {
            context.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        return connection;
    }
}
