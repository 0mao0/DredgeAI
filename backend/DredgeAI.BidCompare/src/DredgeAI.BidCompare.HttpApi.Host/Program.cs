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
using Serilog.Events;

namespace DredgeAI.BidCompare;

public class Program
{
    public async static Task<int> Main(string[] args)
    {
        LoadDotEnv();

        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Async(c => c.File("Logs/logs.txt"))
            .WriteTo.Async(c => c.Console())
            .CreateLogger();

        try
        {
            Log.Information("Starting DredgeAI.BidCompare.HttpApi.Host.");
            var builder = WebApplication.CreateBuilder(args);
            AddEnvConfigOverrides(builder);
            builder.Host.AddAppSettingsSecretsJson()
                .UseAutofac()
                .UseSerilog();
            await builder.AddApplicationAsync<BidCompareHttpApiHostModule>();
            var app = builder.Build();

#if DEBUG
            var llmOptions = app.Services.GetRequiredService<IOptions<LlmOptions>>().Value;
            Log.Information("LLM config: endpoint={Endpoint}, model={Model}, apiKeySet={ApiKeySet}",
                llmOptions.Endpoint, llmOptions.Model, !string.IsNullOrWhiteSpace(llmOptions.ApiKey));
            var anGineerOptions = app.Services.GetRequiredService<IOptions<AnGineerOptions>>().Value;
            Log.Information("AnGIneer config: baseUrl={BaseUrl}, apiKeySet={ApiKeySet}",
                anGineerOptions.BaseUrl, !string.IsNullOrWhiteSpace(anGineerOptions.ApiKey));
#endif

            await app.InitializeApplicationAsync();
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

    /// <summary>.env 中的命名密钥映射到 ABP 配置节（AnGIneer / Llm），供 IOptions 绑定。</summary>
    private static void AddEnvConfigOverrides(WebApplicationBuilder builder)
    {
        var overrides = new Dictionary<string, string?>();
        MapEnv(overrides, "ANGINEER_API_KEY", "AnGIneer:ApiKey");
        MapEnv(overrides, "LLM_API_KEY", "Llm:ApiKey");
        MapEnv(overrides, "LLM_ENDPOINT", "Llm:Endpoint");
        MapEnv(overrides, "LLM_MODEL", "Llm:Model");
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
