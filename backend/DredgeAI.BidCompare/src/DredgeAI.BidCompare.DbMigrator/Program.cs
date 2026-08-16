using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace DredgeAI.BidCompare.DbMigrator;

class Program
{
    static async Task Main(string[] args)
    {
        LoadDotEnv();

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Volo.Abp", LogEventLevel.Warning)
#if DEBUG
                .MinimumLevel.Override("DredgeAI.BidCompare", LogEventLevel.Debug)
#else
                .MinimumLevel.Override("DredgeAI.BidCompare", LogEventLevel.Information)
#endif
                .Enrich.FromLogContext()
            .WriteTo.Async(c => c.File("Logs/logs.txt"))
            .WriteTo.Async(c => c.Console())
            .CreateLogger();

        await CreateHostBuilder(args).RunConsoleAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .AddAppSettingsSecretsJson()
            .ConfigureAppConfiguration((context, config) =>
            {
                // 连接串经环境变量注入（.env 或容器环境），不落 appsettings.json
                var dbConnection = Environment.GetEnvironmentVariable("BIDCOMPARE_DB_CONNECTION");
                if (!string.IsNullOrWhiteSpace(dbConnection))
                {
                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Default"] = dbConnection
                    });
                }
            })
            .ConfigureLogging((context, logging) => logging.ClearProviders())
            .ConfigureServices((hostContext, services) =>
            {
                services.AddHostedService<DbMigratorHostedService>();
            });

    /// <summary>
    /// 读取仓库根目录 .env（本地密钥，不入 git）：从程序目录向上最多 8 层查找。
    /// 仅加载 KEY=VALUE；值支持双/单引号包裹。已存在的同名进程环境变量不会被覆盖。
    /// </summary>
    private static void LoadDotEnv()
    {
        var candidates = new List<string>();
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i <= 8 && dir != null; i++)
        {
            candidates.Add(Path.Combine(dir.FullName, ".env"));
            dir = dir.Parent;
        }
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), ".env"));

        var envPath = candidates.FirstOrDefault(File.Exists);
        if (envPath == null)
        {
            return;
        }

        foreach (var line in File.ReadAllLines(envPath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }
            var eq = trimmed.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }
            var key = trimmed[..eq].Trim();
            var value = trimmed[(eq + 1)..].Trim().Trim('"', '\'');
            if (key.Length == 0 || Environment.GetEnvironmentVariable(key) != null)
            {
                continue;
            }
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
