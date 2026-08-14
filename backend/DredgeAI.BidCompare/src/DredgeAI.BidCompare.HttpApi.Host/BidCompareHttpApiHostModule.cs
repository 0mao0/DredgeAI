using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
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
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using DredgeAI.BidCompare.EntityFrameworkCore;
using DredgeAI.BidCompare.MultiTenancy;
using DredgeAI.BidCompare.Storage;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Microsoft.OpenApi.Models;
using OpenIddict.Validation.AspNetCore;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
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

        Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(options =>
        {
            for (var i = options.JsonSerializerOptions.Converters.Count - 1; i >= 0; i--)
            {
                if (options.JsonSerializerOptions.Converters[i] is System.Text.Json.Serialization.JsonStringEnumConverter)
                {
                    options.JsonSerializerOptions.Converters.RemoveAt(i);
                }
            }
        });

        Configure<S3StorageOptions>(configuration.GetSection("Storage:S3"));
        Configure<LocalStorageOptions>(configuration.GetSection("Storage:Local"));
        if ((configuration["Storage:Provider"] ?? "S3").Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            context.Services.AddSingleton<IFileStorage, LocalFileStorage>();
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
        // AnGIneer 轮询间隔为 5s，服务端 keep-alive 超时也是 5s 级别；
        // 缩短连接池空闲寿命，避免复用已被服务端关闭的旧连接（SocketException 10053）。
        context.Services.AddHttpClient(nameof(HttpAnGineerClient))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionIdleTimeout = TimeSpan.FromSeconds(2),
                PooledConnectionLifetime = TimeSpan.FromSeconds(30),
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

            options.Applications["Angular"].RootUrl = configuration["App:ClientUrl"];
            options.Applications["Angular"].Urls[AccountUrlNames.PasswordReset] = "account/reset-password";
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
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(configuration["App:CorsOrigins"]?
                        .Split(",", StringSplitOptions.RemoveEmptyEntries)
                        .Select(o => o.RemovePostFix("/"))
                        .ToArray() ?? Array.Empty<string>())
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();

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
        if ((context.ServiceProvider.GetRequiredService<IConfiguration>()["Storage:Provider"] ?? "S3")
            .Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            var localStorage = context.ServiceProvider.GetRequiredService<IOptions<LocalStorageOptions>>().Value;
            var root = Path.GetFullPath(localStorage.RootPath);
            Directory.CreateDirectory(root);
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(root),
                RequestPath = "/storage"
            });
        }
        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }
        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization();

        app.UseSwagger();
        app.UseAbpSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "BidCompare API");

            var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
            c.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
            c.OAuthScopes("BidCompare");
        });

        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints();
    }
}
