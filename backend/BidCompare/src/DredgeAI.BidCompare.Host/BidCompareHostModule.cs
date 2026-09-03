using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.Applications;
using DredgeAI.BidCompare.BackgroundJobs;
using DredgeAI.BidCompare.EntityFrameworkCore;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.MeetingBot;
using DredgeAI.BidCompare.Reporting;
using DredgeAI.BidCompare.Storage;
using DredgeAI.BidCompare.Weather;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Caching;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Swashbuckle;
using Volo.Abp.Timing;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;

namespace DredgeAI.BidCompare;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(BidCompareApplicationModule),
    typeof(BidCompareHttpApiModule),
    typeof(BidCompareEntityFrameworkCoreModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpSwashbuckleModule)
)]
public class BidCompareHostModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        
        AbpBackgroundJobsDbProperties.DbTablePrefix = "tab_";
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        Configure<AbpClockOptions>(options =>
        {
            options.Kind = DateTimeKind.Utc;
        });
        ConfigureAuthentication(context, configuration);
        ConfigureUrls(configuration);
        ConfigureConventionalControllers();
        ConfigureVirtualFileSystem(context);
        ConfigureCors(context, configuration);
        ConfigureSwaggerServices(context, configuration);
     
        if (hostingEnvironment.IsDevelopment())
        {
            // 本地联调：user-web 尚无登录/权限链路，关闭 ABP 自动防伪校验；
            // 生产环境仍走完整认证与防伪流程。
            Configure<AbpAntiForgeryOptions>(options => options.AutoValidate = false);
        }

        Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
        {
            for (var i = options.JsonSerializerOptions.Converters.Count - 1; i >= 0; i--)
            {
                if (options.JsonSerializerOptions.Converters[i] is System.Text.Json.Serialization.JsonStringEnumConverter)
                {
                    options.JsonSerializerOptions.Converters.RemoveAt(i);
                }
            }

            // 枚举以 snake_case 字符串序列化（如 done / ai_analyzing），契约自文档化，
            // 避免前端依赖魔法数字；反序列化同时兼容字符串与整数。
            options.JsonSerializerOptions.Converters.Add(
                new System.Text.Json.Serialization.JsonStringEnumConverter(
                    System.Text.Json.JsonNamingPolicy.SnakeCaseLower));
        });

        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "DredgeAI:"; });
        // Shiw 后台任务分叉要求 ApplicationName 非空（f_application_name NOT NULL），
        // ABP 默认 null 会导致入队报 23502；多应用共用一库时按应用名隔离任务。
        Configure<AbpBackgroundJobWorkerOptions>(options =>
        {
            options.ApplicationName = "BidCompare";
        });
        Configure<AbpMultiTenancyOptions>(options =>
        {
            options.IsEnabled = MultiTenancyConsts.IsEnabled;
        });
        Configure<S3StorageOptions>(configuration.GetSection("Storage:S3"));
        Configure<LocalStorageOptions>(configuration.GetSection("Storage:Local"));
        if ((configuration["Storage:Provider"] ?? "S3").Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            // 自注册供签名下载端点校验签名（StorageFileController）
            context.Services.AddSingleton<LocalFileStorage>();
            context.Services.AddSingleton<IFileStorage>(sp => sp.GetRequiredService<LocalFileStorage>());
        }
        else
        {
            context.Services.AddSingleton<IFileStorage, S3FileStorage>();
        }
        Configure<AnGineerPollOptions>(configuration.GetSection("AnGIneer"));
        Configure<AnGineerOptions>(configuration.GetSection("AnGIneer"));
        Configure<AlgoServiceOptions>(configuration.GetSection("AlgoService"));
        Configure<AiGatewayOptions>(configuration.GetSection("AiGateway"));
        Configure<ReportExportOptions>(configuration.GetSection("Export"));
        Configure<LibreOfficeOptions>(configuration.GetSection("LibreOffice"));
        Configure<WatchdogOptions>(configuration.GetSection("Watchdog"));
        Configure<CleanupOptions>(configuration.GetSection("Cleanup"));
        
        Configure<AbpLocalizationOptions>(options =>
        {
            options.Languages.Add(new LanguageInfo("en", "en", "English"));
            options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
        });
        
        // 应用展示顺序存储：JSON 文件持久化（App_Data/app-order.json），后端重启不丢
        context.Services.AddSingleton(sp =>
        {
            var env = sp.GetRequiredService<IHostEnvironment>();
            var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(dataDir);
            return new ApplicationOrderStore(Path.Combine(dataDir, "app-order.json"));
        });
        // 应用目录存储：JSON 文件持久化（App_Data/app-catalog.json），首次运行以内置种子初始化
        context.Services.AddSingleton(sp =>
        {
            var env = sp.GetRequiredService<IHostEnvironment>();
            var dataDir = Path.Combine(env.ContentRootPath, "App_Data");
            Directory.CreateDirectory(dataDir);
            // 种子资源随 HttpApi 项目复制到输出目录，按程序集目录解析
            var seedPath = Path.Combine(AppContext.BaseDirectory, "Resources", "seed-app-catalog.json");
            return new ApplicationCatalogStore(Path.Combine(dataDir, "app-catalog.json"), seedPath);
        });
        Configure<MeetingBotOptions>(configuration.GetSection("MeetingBot"));
        Configure<WeatherOptions>(configuration.GetSection("Weather"));
        // AnGIneer 轮询间隔为 5s，服务端 keep-alive 超时也是 5s 级别，
        // 缩短连接池空闲寿命，避免复用已被服务端关闭的旧连接（SocketException 10053）。
        // 显式 Timeout：默认 100s 不够 200MB 级标书上传，加长到 10 分钟（上限，状态轮询照常快速返回）。
        context.Services.AddHttpClient(nameof(HttpAnGineerClient), (sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<AnGineerOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = TimeSpan.FromMinutes(10);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(2),
                PooledConnectionLifetime = TimeSpan.FromSeconds(30),
            });
        context.Services.AddHttpClient(nameof(HttpCompareAlgoClient), (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AlgoServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        context.Services.AddHttpClient(nameof(HttpLlmGateway), (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<AiGatewayOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
        context.Services.AddHttpClient(nameof(MeetingBotClient), (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<MeetingBotOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(5);
            if (!string.IsNullOrWhiteSpace(options.Key))
            {
                client.DefaultRequestHeaders.Add("X-Meeting-Bot-Key", options.Key);
            }
        });
        context.Services.AddHttpClient(nameof(HttpWeatherClient), (sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        context.Services.AddTransient<IWeatherClient, HttpWeatherClient>();
        context.Services.AddTransient<IMeetingBotClient, MeetingBotClient>();
        context.Services.AddHttpClient();
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context, IConfiguration configuration)
    {
        // 令牌由 Auth 服务（https://localhost:7233/）签发，本服务仅校验 JWT。
        context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddAbpJwtBearer(options =>
            {
                options.Authority = configuration["AuthServer:Authority"];
                options.RequireHttpsMetadata = configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata");
                options.Audience = "DredgeAI";
            });
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
            options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"]?.Split(',') ?? Array.Empty<string>());
        });
    }

    private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<BidCompareDomainSharedModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        $"..{Path.DirectorySeparatorChar}DredgeAI.BidCompare.Domain.Shared"));
                options.FileSets.ReplaceEmbeddedByPhysical<BidCompareDomainModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        $"..{Path.DirectorySeparatorChar}DredgeAI.BidCompare.Domain"));
                options.FileSets.ReplaceEmbeddedByPhysical<BidCompareApplicationContractsModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        $"..{Path.DirectorySeparatorChar}DredgeAI.BidCompare.Application.Contracts"));
                options.FileSets.ReplaceEmbeddedByPhysical<BidCompareApplicationModule>(
                    Path.Combine(hostingEnvironment.ContentRootPath,
                        $"..{Path.DirectorySeparatorChar}DredgeAI.BidCompare.Application"));
            });
        }
    }

    private void ConfigureConventionalControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(BidCompareApplicationModule).Assembly);
        });
    }

    private static void ConfigureSwaggerServices(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAbpSwaggerGenWithOAuth(
            configuration["AuthServer:Authority"]!,
            new Dictionary<string, string>
            {
                    {"DredgeAI", "DredgeAI API"}
            },
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "BidCompare API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
                options.SchemaFilter<DateTimeUtcSchemaFilter>();
            });
    }

    private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                var origins = configuration["App:CorsOrigins"]?
                    .Split(",", StringSplitOptions.RemoveEmptyEntries)
                    .Select(o => o.RemovePostFix("/"))
                    .ToArray() ?? Array.Empty<string>();

                builder
                    .WithOrigins(origins)
                    .WithAbpExposedHeaders()
                    .AllowAnyHeader()
                    .AllowAnyMethod();

                if (hostingEnvironment.IsDevelopment())
                {
                    // 开发环境放宽：允许本地前端（http://localhost:5373 等）与凭证携带
                    builder.SetIsOriginAllowed(_ => true).AllowCredentials();
                }
                else if (origins.Length > 0)
                {
                    // 生产环境收敛：仅精确枚举域名允许携带凭证
                    builder.AllowCredentials();
                }
            });
        });
    }

    public override async System.Threading.Tasks.Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        // 启动诊断：确认 AnGIneer API Key 是否配置（密钥内容一律不落日志）
        var anGineerOptions = context.ServiceProvider.GetRequiredService<IOptions<AnGineerOptions>>().Value;
        app.ApplicationServices.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .CreateLogger("AnGineerConfig")
            .LogInformation("AnGIneer ApiKey 配置: {State}；BaseUrl: {BaseUrl}",
                string.IsNullOrWhiteSpace(anGineerOptions.ApiKey) ? "未配置" : "已配置",
                anGineerOptions.BaseUrl);

        // 启动检查：用量上报端点 fail-closed，未配置令牌时必须显式告警（防止共享/生产环境静默裸奔）
        var aiGatewayOptions = context.ServiceProvider.GetRequiredService<IOptions<AiGatewayOptions>>().Value;
        if (string.IsNullOrWhiteSpace(aiGatewayOptions.IngestToken))
        {
            app.ApplicationServices.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
                .CreateLogger("AiGatewayConfig")
                .LogWarning(
                    "AiGateway:IngestToken 未配置，POST /api/ai-gateway/usage-records 已 fail-closed 拒绝所有上报；" +
                    "共享/生产环境必须配置 AI_GATEWAY_INGEST_TOKEN");
        }

        // 反向代理路径前缀（如 /bidcompare）：剥离前缀后再进入业务管道
        var pathBase = context.ServiceProvider.GetRequiredService<IConfiguration>()["App:PathBase"];
        if (!string.IsNullOrWhiteSpace(pathBase))
        {
            app.UsePathBase(pathBase);
        }

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization(opt => { opt.SetDefaultCulture("zh-Hans"); });

        app.UseCorrelationId();
        app.UseStaticFiles();
        // 注意：本地存储不再以匿名静态文件挂载到 /storage（S2），
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseUnitOfWork();
        app.UseAuthorization();

        var swaggerEnabled = env.IsDevelopment()
            || context.ServiceProvider.GetRequiredService<IConfiguration>().GetValue<bool>("Swagger:Enabled");
        if (swaggerEnabled)
        {
            app.UseSwagger();
            app.UseAbpSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "BidCompare API");

                var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
                c.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
                c.OAuthScopes("DredgeAI");
            });
        }

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();

        // 卡死看门狗（M9）：Parsing/Comparing/Analyzing 中间态超时巡检
        var watchdog = context.ServiceProvider.GetRequiredService<IOptions<WatchdogOptions>>().Value;
        if (watchdog.Enabled)
        {
            await context.AddBackgroundWorkerAsync<StuckTaskWatchdogWorker>();
        }

        // 孤儿数据清扫：超时草稿会话与过期导出文件
        var cleanup = context.ServiceProvider.GetRequiredService<IOptions<CleanupOptions>>().Value;
        if (cleanup.Enabled)
        {
            await context.AddBackgroundWorkerAsync<OrphanCleanupWorker>();
        }

        // 进程重启恢复：Parsing 且已有 AnGIneer doc_id 的文档重新入队，由解析任务查状态并 resume。
        using (var scope = context.ServiceProvider.CreateScope())
        {
            var recovery = scope.ServiceProvider.GetRequiredService<ParseRecoveryService>();
            await recovery.RecoverAsync();
        }
    }
}
