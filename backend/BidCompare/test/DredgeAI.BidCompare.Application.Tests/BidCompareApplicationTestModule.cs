using DredgeAI.BidCompare.EntityFrameworkCore;
using Shiw.Abp.BaseEntityFrameworkCore;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.Storage;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.Reporting;
using DredgeAI.BidCompare.MeetingBot;
using DredgeAI.BidCompare.Weather;
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
        // 生产环境 IBackgroundJobManager 按作用域注册（DefaultBackgroundJobManager 依赖作用域内 DbContext/ObjectMapper）；
        // 看门狗曾因构造函数注入该服务、绑定到已释放作用域而崩溃，测试用 Scoped 复现该场景。
        context.Services.Replace(ServiceDescriptor.Scoped<IBackgroundJobManager, RecordingBackgroundJobManager>());
        context.Services.Replace(ServiceDescriptor.Singleton<IFileStorage, InMemoryFileStorage>());
        context.Services.Replace(ServiceDescriptor.Singleton<IAnGineerClient, FakeAnGineerClient>());
        context.Services.Replace(ServiceDescriptor.Singleton<ICompareAlgoClient, FakeCompareAlgoClient>());
        context.Services.Replace(ServiceDescriptor.Singleton<ILlmGateway, FakeLlmGateway>());
        context.Services.Replace(ServiceDescriptor.Singleton<IPdfConverter, FakePdfConverter>());
        context.Services.Replace(ServiceDescriptor.Singleton<IWordReportRenderer, FakeWordReportRenderer>());
        context.Services.Replace(ServiceDescriptor.Singleton<IMeetingBotClient, FakeMeetingBotClient>());
        context.Services.Replace(ServiceDescriptor.Singleton<IWeatherClient, FakeWeatherClient>());
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

        using (var context = new BidCompareDbContext(options, new DefaultShiwDbContextHandler()))
        {
            context.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        return connection;
    }
}
