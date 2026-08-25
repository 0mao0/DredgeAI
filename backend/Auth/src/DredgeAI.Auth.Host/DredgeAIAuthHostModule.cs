using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;
using OpenIddict.Validation.AspNetCore;
using Shiw.Abp.AuditLogging.EntityFrameworkCore;
using Shiw.Abp.FeatureManagement.EntityFrameworkCore;
using Shiw.Abp.Identity.EntityFrameworkCore;
using Shiw.Abp.OpenIddict.EntityFrameworkCore;
using Shiw.Abp.PermissionManagement.EntityFrameworkCore;
using Shiw.Abp.SettingManagement.EntityFrameworkCore;
using Shiw.Abp.TenantManagement.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Basic.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Auditing;
using Volo.Abp.Autofac;
using Volo.Abp.Caching;
using Volo.Abp.Data;
using Volo.Abp.Emailing;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.SettingManagement;
using Volo.Abp.Swashbuckle;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Volo.Abp.UI.Navigation.Urls;

namespace DredgeAI;

[DependsOn(
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpAccountApplicationModule),
    typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpAspNetCoreMvcUiBasicThemeModule),
    typeof(AbpAutofacModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(ShiwAuditLoggingEntityFrameworkCoreModule),
    typeof(ShiwIdentityEntityFrameworkCoreModule),
    typeof(ShiwOpenIddictEntityFrameworkCoreModule),
    typeof(ShiwPermissionManagementEntityFrameworkCoreModule),
    typeof(ShiwSettingManagementEntityFrameworkCoreModule),
    typeof(ShiwFeatureManagementEntityFrameworkCoreModule),
    typeof(ShiwTenantManagementEntityFrameworkCoreModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpPermissionManagementDomainIdentityModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(DredgeAIAuthApplicationModule),
    typeof(DredgeAIAuthHttpApiModule),
    typeof(DredgeAIAuthEntityFrameworkCoreModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpSwashbuckleModule)
)]
public class DredgeAIAuthHostModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        AbpCommonDbProperties.DbTablePrefix = "tab";
        AbpIdentityDbProperties.DbTablePrefix = "tab_identity";
        AbpOpenIddictDbProperties.DbTablePrefix = "tab_openid_dict";

        var env = context.Services.GetHostingEnvironment();
        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("DredgeAI");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });
        if (!env.IsDevelopment())
        {
            Configure<OpenIddictServerAspNetCoreOptions>(options =>
            {
                // 开发环境有开发证书，正式环境在容器内只暴露http端口，对外通过网关暴露https
                options.DisableTransportSecurityRequirement = true;
            });
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.SetAccessTokenLifetime(TimeSpan.FromDays(1));
                serverBuilder.AddProductionEncryptionAndSigningCertificate("openiddict.pfx",
                    "HNHAYRldSNI01qCuMAYxr");
            });
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        Configure<AbpClockOptions>(options => { options.Kind = DateTimeKind.Utc; });

        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults
            .AuthenticationScheme);

        // 固定 OIDC issuer 为网关统一入口（本地 https://localhost:7127，生产 https://<域名>/gateway）。
        // issuer 仅影响 iss 声明与发现文档 issuer 字段；发现文档各端点地址由 OpenIddict 按请求 BaseUri
        // 推导（与显式 issuer 无关），公网端点地址依赖管道最前的 UseForwardedHeaders 还原转发头。
        // 不配置 IssuerUri（如经域名子路径直连且转发头完整）则按请求推导。
        var issuerUri = configuration["OpenIddict:IssuerUri"];
       if (!string.IsNullOrWhiteSpace(issuerUri))
        {
            Configure<OpenIddictServerOptions>(options =>
            {
                options.Issuer = new Uri(issuerUri);
            });
        }

        Configure<AbpDbContextOptions>(options => { options.UseNpgsql(); });

        context.Services.AddAbpSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "DredgeAI API",
                Version = "v1",
                Description = "All DateTime fields use UTC with Z suffix (ISO 8601). Example: 2026-07-12T02:00:00Z"
            });
            options.DocInclusionPredicate((docName, description) => true);
            options.CustomSchemaIds(type => type.FullName);
            options.SchemaFilter<DateTimeUtcSchemaFilter>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Languages.Add(new LanguageInfo("en", "en", "English"));
            options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
        });

        // 修复 BasicTheme 在 PathBase 子路径部署（生产 /gateway）下 googlefonts.css 404：
        // 上游将其标记为 external（new BundleFile(path, true)），tag helper 原样输出根绝对路径，
        // 不经 Url.Content("~/") 拼接 PathBase。该文件仅一行 @import 到 Google Fonts CDN，
        // 故移除主题贡献者，恢复 layout.css 并让浏览器直连 CDN（external 原样输出，与 PathBase 无关）。
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(BasicThemeBundles.Styles.Global, bundle =>
            {
                bundle.Contributors.Remove<BasicThemeGlobalStyleContributor>();
                bundle.AddFiles("/themes/basic/layout.css");
                bundle.AddExternalFiles(
                    "https://fonts.googleapis.com/css2?family=Lexend:wght@100..900&family=Poppins:ital,wght@0,100;0,200;0,300;0,400;0,500;0,600;0,700;0,800;0,900;1,100;1,200;1,300;1,400;1,500;1,600;1,700;1,800;1,900&display=swap");
            });
        });

        Configure<AbpAuditingOptions>(options =>
        {
            //options.IsEnabledForGetRequests = true;
            options.ApplicationName = "AuthServer";
        });

        Configure<AppUrlOptions>(options => { options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"]; });

        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "DredgeAI:"; });

        Configure<AbpMultiTenancyOptions>(options => { options.IsEnabled = MultiTenancyConsts.IsEnabled; });


        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(
                        configuration["App:CorsOrigins"]?
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.RemovePostFix("/"))
                            .ToArray() ?? Array.Empty<string>()
                    )
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

#if DEBUG
        context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
#endif
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();
        var configuration = context.GetConfiguration();
        var basePath = configuration.GetValue<string>("App:BasePath") ?? "/";

        // 还原网关转发头（For/Proto/Host/Prefix），必须最先执行：
        // OpenIddict 发现文档的端点地址按请求 BaseUri（Scheme+Host+PathBase）推导，
        // 不还原 Host/Prefix 时端点会变成 Auth 内网地址（issuer 由 OpenIddict:IssuerUri 固定，不受影响）。
        app.UseForwardedHeaders();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseErrorPage();
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UsePathBase(new PathString(basePath));
        app.UseCorrelationId();
        app.MapAbpStaticAssets();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        // if (MultiTenancyConsts.IsEnabled)
        // {
        //     app.UseMultiTenancy();
        // }

        app.UseAbpRequestLocalization(opt => { opt.SetDefaultCulture("zh-Hans"); });
        app.UseAuthorization();
        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"{basePath}swagger/v1/swagger.json", "Support APP API");
        });
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();

        await SeedData(context);
    }

    private async Task SeedData(ApplicationInitializationContext context)
    {
        using var scope = context.ServiceProvider.CreateScope();
        await scope.ServiceProvider
            .GetRequiredService<IDataSeeder>()
            .SeedAsync(new DataSeedContext()
                .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, "admin@localhost.com")
                .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, "DVnXFsq%1vNczqqA")
            );
    }
}