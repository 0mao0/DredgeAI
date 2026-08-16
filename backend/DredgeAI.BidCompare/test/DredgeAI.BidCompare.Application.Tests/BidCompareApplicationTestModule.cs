using DredgeAI.BidCompare.EntityFrameworkCore;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.Storage;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.Reporting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.BackgroundJobs;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace DredgeAI.BidCompare;

[DependsOn(
    typeof(BidCompareApplicationModule),
    typeof(BidCompareEntityFrameworkCoreModule),
    typeof(BidCompareDomainTestModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class BidCompareApplicationTestModule : AbpModule
{
    private SqliteConnection? _sqliteConnection;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddAlwaysDisableUnitOfWorkTransaction();
        context.Services.Replace(ServiceDescriptor.Singleton<IBackgroundJobManager, RecordingBackgroundJobManager>());
        context.Services.Replace(ServiceDescriptor.Singleton<IFileStorage, InMemoryFileStorage>());
        context.Services.Replace(ServiceDescriptor.Singleton<IAnGineerClient, FakeAnGineerClient>());
        context.Services.Replace(ServiceDescriptor.Singleton<ICompareAlgoClient, FakeCompareAlgoClient>());
        context.Services.Replace(ServiceDescriptor.Singleton<ILlmGateway, FakeLlmGateway>());
        context.Services.Replace(ServiceDescriptor.Singleton<IPdfConverter, FakePdfConverter>());
        context.Services.Replace(ServiceDescriptor.Singleton<IWordReportRenderer, FakeWordReportRenderer>());
        // [Task8] IAnGineerClient / [Task9] ICompareAlgoClient / [Task11] ILlmGateway / [Task14] IPdfConverter 的 Fake 在此追加

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

        var options = new DbContextOptionsBuilder<BidCompareDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new BidCompareDbContext(options))
        {
            context.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        return connection;
    }
}
