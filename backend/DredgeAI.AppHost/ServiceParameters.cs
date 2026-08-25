using Aspire.Hosting.ApplicationModel;

namespace DredgeAI.AppHost;

/// <summary>
/// 集中定义所有发布参数（仅 aspire publish 时被引用为 ${VAR} 占位符；本地运行不触发值检查）。
/// 部署时通过 .env 注入实际值。
/// </summary>
public sealed class ServiceParameters
{
    // ============================================================================
    // Dashboard
    // ============================================================================

    /// <summary>Dashboard 登录 token（BrowserToken 模式，部署时通过 .env 注入 DASHBOARD_BROWSER_TOKEN）</summary>
    public IResourceBuilder<ParameterResource> DashboardBrowserToken { get; }

    // ============================================================================
    // Base 服务：签名文件 URL 的站点前缀（Shiw.File AddFileOptions WebSite）
    // 文件 API 经 边缘反代 → gateway（/api/file-management 路由）→ base-service，取域名根
    // ============================================================================

    public IResourceBuilder<ParameterResource> BaseFileWebSiteUrl { get; }

    // ============================================================================
    // 公共配置
    // ============================================================================

    /// <summary>前端可访问的 CORS 源列表（逗号分隔）</summary>
    public IResourceBuilder<ParameterResource> CorsOrigins { get; }

    /// <summary>Auth 服务：OpenIddict 客户端的 RootUrl（指向前端 Base.Host 的对外地址）</summary>
    public IResourceBuilder<ParameterResource> OpenIddictRootUrl { get; }

    /// <summary>Auth 服务：OIDC issuer 固定值（= 网关公网入口，前端经网关登录时 oidc 客户端 issuer 校验一致；不设置则按请求推导）</summary>
    public IResourceBuilder<ParameterResource> OpenIddictIssuerUri { get; }

    // ============================================================================
    // Base 服务：Minio 对象存储配置
    // ============================================================================

    public IResourceBuilder<ParameterResource> MinioEndpoint { get; }
    public IResourceBuilder<ParameterResource> MinioBucket { get; }
    public IResourceBuilder<ParameterResource> MinioAccessKey { get; }
    public IResourceBuilder<ParameterResource> MinioSecretKey { get; }

    public ServiceParameters(IDistributedApplicationBuilder builder)
    {
        DashboardBrowserToken = builder.AddParameter("dashboard-browser-token", secret: true);

        BaseFileWebSiteUrl = builder.AddParameter("base-file-web-site-url");

        CorsOrigins = builder.AddParameter("cors-origins");
        OpenIddictRootUrl = builder.AddParameter("openiddict-root-url");
        OpenIddictIssuerUri = builder.AddParameter("openiddict-issuer-uri");

        MinioEndpoint = builder.AddParameter("minio-endpoint");
        MinioBucket = builder.AddParameter("minio-bucket");
        MinioAccessKey = builder.AddParameter("minio-access-key", secret: true);
        MinioSecretKey = builder.AddParameter("minio-secret-key", secret: true);

    }
}
