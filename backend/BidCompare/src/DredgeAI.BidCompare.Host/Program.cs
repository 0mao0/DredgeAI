using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.AnGineer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
namespace DredgeAI.BidCompare;

public class Program
{
    /// <summary>仓库根目录（由 .env 所在目录推断），用于把运行时数据统一落到根级 data/。</summary>
    private static string? _repoRoot;

    public async static Task<int> Main(string[] args)
    {
        LoadDotEnv();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .CreateLogger();

        try
        {
            Log.Information("Starting DredgeAI.BidCompare.Host.");
            var builder = WebApplication.CreateBuilder(args);
            AddEnvConfigOverrides(builder);
            builder.Host.AddAppSettingsSecretsJson()
                .UseAutofac()
                .UseSerilog();
            builder.AddServiceDefaults();
            await builder.AddApplicationAsync<BidCompareHostModule>();
            var app = builder.Build();

#if DEBUG
            var aiGatewayOptions = app.Services.GetRequiredService<IOptions<AiGatewayOptions>>().Value;
            Log.Information("AI Gateway config: baseUrl={BaseUrl}, apiTokenSet={ApiTokenSet}",
                aiGatewayOptions.BaseUrl, !string.IsNullOrWhiteSpace(aiGatewayOptions.ApiToken));
            var anGineerOptions = app.Services.GetRequiredService<IOptions<AnGineerOptions>>().Value;
            Log.Information("AnGIneer config: baseUrl={BaseUrl}, apiKeySet={ApiKeySet}",
                anGineerOptions.BaseUrl, !string.IsNullOrWhiteSpace(anGineerOptions.ApiKey));
#endif

            await app.InitializeApplicationAsync();
            app.UseRequestTimeouts();
            app.UseOutputCache();
            app.MapDefaultEndpoints();
            await app.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            if (ex is HostAbortedException)
            {
                throw;
            }

            Log.Fatal(ex, "Host terminated unexpectedly!");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

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
        _repoRoot = Path.GetDirectoryName(envPath);

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

    /// <summary>.env/环境变量中的命名密钥映射到 ABP 配置节，供 IOptions 绑定；密钥不落 appsettings.json。</summary>
    private static void AddEnvConfigOverrides(WebApplicationBuilder builder)
    {
        var overrides = new Dictionary<string, string?>();
        MapEnv(overrides, "ANGINEER_API_KEY", "AnGIneer:ApiKey");
        MapEnv(overrides, "AI_GATEWAY_BASE_URL", "AiGateway:BaseUrl");
        MapEnv(overrides, "AI_GATEWAY_API_TOKEN", "AiGateway:ApiToken");
        MapEnv(overrides, "AI_GATEWAY_INGEST_TOKEN", "AiGateway:IngestToken");
        MapEnv(overrides, "BIDCOMPARE_DB_CONNECTION", "ConnectionStrings:Default");
        MapEnv(overrides, "STORAGE_S3_ACCESSKEY", "Storage:S3:AccessKey");
        MapEnv(overrides, "STORAGE_S3_SECRETKEY", "Storage:S3:SecretKey");
        MapEnv(overrides, "STORAGE_LOCAL_SIGNING_SECRET", "Storage:Local:SigningSecret");
        MapEnv(overrides, "STORAGE_LOCAL_ROOT", "Storage:Local:RootPath");
        MapEnv(overrides, "STRING_ENCRYPTION_PASSPHRASE", "StringEncryption:DefaultPassPhrase");
        MapEnv(overrides, "AUTH_REQUIRE_HTTPS_METADATA", "AuthServer:RequireHttpsMetadata");
        MapEnv(overrides, "SWAGGER_ENABLED", "Swagger:Enabled");
        // monorepo 约定：未显式指定时，本地文件存储统一落在仓库根 data/storage
        // （与启动脚本、PostgreSQL、日志共用同一 data/ 根目录，便于备份与迁移）。
        if (!overrides.ContainsKey("Storage:Local:RootPath") && _repoRoot != null)
        {
            overrides["Storage:Local:RootPath"] = Path.Combine(_repoRoot, "data", "storage");
        }
        if (overrides.Count > 0)
        {
            builder.Configuration.AddInMemoryCollection(overrides);
        }
    }

    private static void MapEnv(Dictionary<string, string?> target, string envKey, string configKey)
    {
        var value = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[configKey] = value;
        }
    }
}
