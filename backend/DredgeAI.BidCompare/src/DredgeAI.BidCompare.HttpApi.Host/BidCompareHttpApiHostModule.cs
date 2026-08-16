using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using DredgeAI.BidCompare.AI;
using DredgeAI.BidCompare.Analysis;
using DredgeAI.BidCompare.AnGineer;
using DredgeAI.BidCompare.BackgroundJobs;
using DredgeAI.BidCompare.Exports;
using DredgeAI.BidCompare.Reporting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DredgeAI.BidCompare.EntityFrameworkCore;
using DredgeAI.BidCompare.MultiTenancy;
using DredgeAI.BidCompare.Storage;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Microsoft.OpenApi.Models;
using OpenIddict.Validation.AspNetCore;
using Volo.Abp;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Swashbuckle;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;

namespace DredgeAI.BidCompare;

[DependsOn(
    typeof(BidCompareHttpApiModule),
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMultiTenancyModule),
    typeof(BidCompareApplicationModule),
    typeof(BidCompareEntityFrameworkCoreModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpSwashbuckleModule)
)]
public class BidCompareHttpApiHostModule : AbpModule
{
    /// <summary>开发环境本地联调使用的 admin 用户 Id（启动时查询一次）。</summary>
    private static Guid? _devAdminUserId;

    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("BidCompare");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        ConfigureAuthentication(context);
        ConfigureBundles();
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
        Configure<LlmOptions>(configuration.GetSection("Llm"));
        Configure<ReportExportOptions>(configuration.GetSection("Export"));
        Configure<LibreOfficeOptions>(configuration.GetSection("LibreOffice"));
        Configure<WatchdogOptions>(configuration.GetSection("Watchdog"));
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
        context.Services.AddHttpClient();
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context)
    {
        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }

    private void ConfigureBundles()
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                LeptonXLiteThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                }
            );
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
                    {"BidCompare", "BidCompare API"}
            },
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "BidCompare API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
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

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

        // 启动诊断：确认实际生效的 AnGIneer API Key（只打前缀，避免泄露完整密钥）
        var anGineerOptions = context.ServiceProvider.GetRequiredService<IOptions<AnGineerOptions>>().Value;
        var key = anGineerOptions.ApiKey;
        app.ApplicationServices.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>()
            .CreateLogger("AnGineerConfig")
            .LogInformation("AnGIneer ApiKey 配置: {State}；BaseUrl: {BaseUrl}",
                string.IsNullOrWhiteSpace(key)
                    ? "未配置（空）"
                    : $"已配置（前缀 {key[..Math.Min(8, key.Length)]}...，长度 {key.Length}）",
                anGineerOptions.BaseUrl);

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
        }

        app.UseCorrelationId();
        app.UseStaticFiles();
        // 娉ㄦ剰锛氭湰鍦板瓨鍌ㄤ笉鍐嶄互鍖垮悕闈欐€佹枃浠舵寕杞藉埌 /storage锛圫2锛夛紝
        // 注意：本地存储不再以匿名静态文件挂载到 /storage（S2），
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        if (env.IsDevelopment())
        {
            // 本地联调：user-web 尚未接入 ABP 登录，自动以 admin 身份访问；
            // 生产环境不启用，仍要求真实令牌。
            app.Use(async (httpContext, next) =>
            {
                if (httpContext.User.Identity?.IsAuthenticated != true && _devAdminUserId.HasValue)
                {
                    var claims = new List<Claim>
                    {
                        new(AbpClaimTypes.UserId, _devAdminUserId.Value.ToString()),
                        new(AbpClaimTypes.UserName, "admin"),
                        new(AbpClaimTypes.Role, "admin"),
                    };
                    httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Development"));
                }

                await next();
            });
        }

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }
        app.UseUnitOfWork();
        app.UseDynamicClaims();
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
                c.OAuthScopes("BidCompare");
            });
        }

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
    }

    public override async System.Threading.Tasks.Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var env = context.GetEnvironment();
        if (env.IsDevelopment())
        {
            using var scope = context.ServiceProvider.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<IdentityUserManager>();
            var admin = await userManager.FindByNameAsync("admin");
            _devAdminUserId = admin?.Id;
        }

        OnApplicationInitialization(context);

        // 卡死看门狗（M9）：Parsing/Comparing/Analyzing 中间态超时巡检
        var watchdog = context.ServiceProvider.GetRequiredService<IOptions<WatchdogOptions>>().Value;
        if (watchdog.Enabled)
        {
        // 卡死看门狗（M9）：Parsing/Comparing/Analyzing 中间态超时巡检
        }

        // 进程重启恢复：Parsing 且已有 AnGIneer doc_id 的文档重新入队，由解析任务查状态并 resume。
        using (var scope = context.ServiceProvider.CreateScope())
        {
            var recovery = scope.ServiceProvider.GetRequiredService<ParseRecoveryService>();
            await recovery.RecoverAsync();
        }
    }
}
