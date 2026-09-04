using System.Threading.RateLimiting;
using DredgeAI.Gateway;
using DredgeAI.Gateway.EntityFrameworkCore;
using DredgeAI.Gateway.Proxying;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.AspNetCore.Authentication.JwtBearer;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Autofac;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.Modularity;
using Volo.Abp.Threading;

namespace DredgeAI;

[DependsOn(
    typeof(AbpAspNetCoreAuthenticationJwtBearerModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMvcModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(GatewayApplicationModule),
    typeof(GatewayHttpApiModule),
    typeof(GatewayEntityFrameworkCoreModule)
)]
public class DredgeAIGatewayHostModule : AbpModule
{
    /// <summary>代理端点统一使用的限流策略名（appsettings.json RateLimiting 节可调参）。</summary>
    public const string ProxyRateLimitPolicy = "proxy-fixed";

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        // 接入认证中心：验证 Auth 服务颁发的 JWT（配置与其他服务一致）
        context.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddAbpJwtBearer(options =>
            {
                options.Authority = configuration["AuthServer:Authority"];
                options.RequireHttpsMetadata = configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata");
                options.Audience = "DredgeAI";
            });

        // YARP：路由/集群来自 DB（DatabaseProxyConfigProvider 由 GatewayApplicationModule 注册，支持热重载）；
        // appsettings 的 ReverseProxy 节仅作为首次启动的种子数据源
        context.Services.AddReverseProxy();

        // 请求限流：按客户端 IP 分区的固定窗口；仅作用于代理端点（不健康检查），不设 GlobalLimiter
        var permitLimit = configuration.GetValue("RateLimiting:PermitLimit", 100);
        var windowSeconds = configuration.GetValue("RateLimiting:WindowSeconds", 10);
        var queueLimit = configuration.GetValue("RateLimiting:QueueLimit", 0);
        context.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(ProxyRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = queueLimit
                    }));
        });

        // CORS：网关作为前端入口，CORS 策略与现有服务同构（WithAbpExposedHeaders 来自 Volo.Abp.AspNetCore）
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
    }

    public override Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();

        // 首次启动：路由表为空时从 ReverseProxy 配置节导入 DB；随后显式 Reload 保证 YARP 快照就位
        AsyncHelper.RunSync(async () =>
        {
            using var scope = context.ServiceProvider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IDataSeeder>().SeedAsync();
        });
        context.ServiceProvider.GetRequiredService<DatabaseProxyConfigProvider>().Reload();

        // 还原 nginx 转发头（For/Proto/Host/Prefix），必须最先执行：
        // YARP 默认以 Set 动作从自身请求状态生成 X-Forwarded-* 传给下游，
        // Auth 据此还原公网地址；同时限流分区才能拿到真实客户端 IP。
        app.UseForwardedHeaders();

        var env = context.GetEnvironment();
        

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseCorrelationId();

        // Path base for deployment path prefix (e.g. /gateway). Empty in local dev.
        var pathBase =context.GetConfiguration()["PathBase"];
        if (!string.IsNullOrEmpty(pathBase))
        {
            app.UsePathBase(new PathString(pathBase));
        }

        app.UseRouting();
        app.UseCors();
        app.UseAuthentication();
        app.UseAbpRequestLocalization(opt => { opt.SetDefaultCulture("zh-Hans"); });
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseAbpSerilogEnrichers();
        app.UseConfiguredEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapReverseProxy().RequireRateLimiting(ProxyRateLimitPolicy);
        });
        return Task.CompletedTask;
    }
}
