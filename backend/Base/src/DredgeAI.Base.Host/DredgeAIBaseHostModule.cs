using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi;
using Shiw.Abp.AuditLogging.EntityFrameworkCore;
using Shiw.Abp.FeatureManagement.EntityFrameworkCore;
using Shiw.Abp.Identity.EntityFrameworkCore;
using Shiw.Abp.PermissionManagement.EntityFrameworkCore;
using Shiw.Abp.SettingManagement.EntityFrameworkCore;
using Shiw.Abp.TenantManagement.EntityFrameworkCore;
using Shiw.File;
using Shiw.File.BlobStoring.Minio;
using Shiw.File.Domain;
using DredgeAI.Permissions;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.AntiForgery;
using Volo.Abp.AspNetCore.Mvc.Libs;
using Volo.Abp.AspNetCore.Mvc.UI.MultiTenancy;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Auditing;
using Volo.Abp.Authorization.Permissions;
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
using Volo.Abp.PermissionManagement;
using Volo.Abp.PermissionManagement.HttpApi;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.SettingManagement;
using Volo.Abp.Swashbuckle;
using Volo.Abp.TenantManagement;
using Volo.Abp.Timing;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;

namespace DredgeAI;

[DependsOn(
    typeof(AbpAccountApplicationModule),
    typeof(AbpAccountHttpApiModule),
    typeof(AbpAspNetCoreMvcUiMultiTenancyModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpAutofacModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(ShiwAuditLoggingEntityFrameworkCoreModule),
    typeof(ShiwIdentityEntityFrameworkCoreModule),
    typeof(ShiwPermissionManagementEntityFrameworkCoreModule),
    typeof(ShiwSettingManagementEntityFrameworkCoreModule),
    typeof(ShiwFeatureManagementEntityFrameworkCoreModule),
    typeof(ShiwTenantManagementEntityFrameworkCoreModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpIdentityHttpApiModule),
    typeof(AbpPermissionManagementDomainIdentityModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpPermissionManagementHttpApiModule),
    typeof(AbpSettingManagementApplicationModule),
    typeof(AbpSettingManagementHttpApiModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpFeatureManagementHttpApiModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpTenantManagementHttpApiModule),
    typeof(FileApplicationModule),
    typeof(FileBlobStoringMinioModule),
    typeof(DredgeAIBaseApplicationModule),
    typeof(DredgeAIBaseHttpApiModule),
    typeof(DredgeAIBaseEntityFrameworkCoreModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpSwashbuckleModule)
)]
public class DredgeAIBaseHostModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        AbpCommonDbProperties.DbTablePrefix = "tab";
        AbpIdentityDbProperties.DbTablePrefix="tab_identity";
        DredgeAIBaseDbProperties.DbTablePrefix="tab";
        FileDbProperties.DbTablePrefix = "tab";
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();
        context.Services.AddFileOptions(options =>
        {
            options.EnableSignedUrl = true;
            options.IsRouterStyleV2 = false;
            options.IsAppendFileRoutePath = true;
            // 签名文件 URL 站点前缀（独立配置 App:FileWebSiteUrl）：发布环境注入域名根，经 gateway 文件路由可达
            options.WebSite = configuration.GetValue<string>("App:FileWebSiteUrl");
            options.AccessTokenSecret=configuration.GetValue<string>("App:FileUrlSecretKey");
        });

        Configure<AbpClockOptions>(options =>
        {
            options.Kind = DateTimeKind.Utc;
        });

        Configure<AbpPermissionOptions>(options =>
        {
            options.DefinitionProviders.Remove<SettingManagementPermissionDefinitionProvider>();
            options.DefinitionProviders.Remove<AbpTenantManagementPermissionDefinitionProvider>();
            options.DefinitionProviders.Remove<FeaturePermissionDefinitionProvider>();
            options.DefinitionProviders.Remove<IdentityPermissionDefinitionProvider>();

        });
        
        Configure<PermissionManagementOptions>(options =>
        {
            
            options.ProviderPolicies["R"] = DredgeAIBasePermissions.Roles.ManagePermissions;
            options.ProviderPolicies["U"] = DredgeAIBasePermissions.Users.ManagePermissions;
        });

        Configure<AbpMvcLibsOptions>(options =>
        {
            options.CheckLibs = false;
        });
        Configure<AbpAntiForgeryOptions>(options =>
        {
            options.AutoValidate = false;
        });

        Configure<AbpDbContextOptions>(options => { options.UseNpgsql(); });

        context.Services.AddAbpSwaggerGenWithOAuth(
            configuration["AuthServer:Authority"]!,
            new Dictionary<string, string>
            {
                {"DredgeAI", "DredgeAI API"}
            },
            options =>
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

            // 加载输出目录中所有 Shiw.*.xml 注释文件（包含本模块项目及 NuGet 包）
            var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "Shiw.*.xml");
            foreach (var xmlFile in xmlFiles)
            {
                options.IncludeXmlComments(xmlFile);
            }
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Languages.Add(new LanguageInfo("en", "en", "English"));
            options.Languages.Add(new LanguageInfo("zh-Hans", "zh-Hans", "简体中文"));
        });

        Configure<AbpAuditingOptions>(options =>
        {
            //options.IsEnabledForGetRequests = true;
            options.ApplicationName = "BaseServer";
        });

        Configure<AppUrlOptions>(options => { options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"]; });

        context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddAbpJwtBearer(options =>
            {
                options.Authority = configuration["AuthServer:Authority"];
                options.RequireHttpsMetadata = configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata");
                options.Audience = "DredgeAI";
            });
        
        Configure<AbpDistributedCacheOptions>(options => { options.KeyPrefix = "DredgeAI:"; });

        Configure<AbpMultiTenancyOptions>(options => { options.IsEnabled = MultiTenancyConsts.IsEnabled; });
        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<DredgeAIBaseDomainSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath,
                    $"..{Path.DirectorySeparatorChar}DredgeAI.Base.Domain.Shared"));
                options.FileSets.ReplaceEmbeddedByPhysical<DredgeAIBaseDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath,
                    $"..{Path.DirectorySeparatorChar}DredgeAI.Base.Domain"));
                options.FileSets.ReplaceEmbeddedByPhysical<DredgeAIBaseApplicationContractsModule>(Path.Combine(hostingEnvironment.ContentRootPath,
                    $"..{Path.DirectorySeparatorChar}DredgeAI.Base.Application.Contracts"));
                options.FileSets.ReplaceEmbeddedByPhysical<DredgeAIBaseApplicationModule>(Path.Combine(hostingEnvironment.ContentRootPath,
                    $"..{Path.DirectorySeparatorChar}DredgeAI.Base.Application"));
            });
        }

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

    public override Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();
        var configuration = context.GetConfiguration();
        var basePath = configuration.GetValue<string>("App:BasePath") ?? "/";

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UsePathBase(new PathString(basePath));
        app.UseCorrelationId();
        app.MapAbpStaticAssets();
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }
        app.UseAbpRequestLocalization(opt => { opt.SetDefaultCulture("zh-Hans"); });
        app.UseAuthorization();
        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"{basePath}swagger/v1/swagger.json", "Support APP API");

            options.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
            options.OAuthScopes("DredgeAI");
        });
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
        return Task.CompletedTask;
    }

    public override async Task OnPostApplicationInitializationAsync(ApplicationInitializationContext context)
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