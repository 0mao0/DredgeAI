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

    // ============================================================================
    // Python 服务
    // ============================================================================

    /// <summary>模型服务（5 个）共享鉴权头 X-Meeting-Bot-Key 的密钥；默认 dev-key 与本地/各 .env.example 一致</summary>
    public IResourceBuilder<ParameterResource> MeetingBotKey { get; }
    /// <summary>ai-gateway 入站校验令牌（空 = 关闭鉴权，本地默认）；BidCompare AiGateway:ApiToken 需一致</summary>
    public IResourceBuilder<ParameterResource> AiGatewayApiToken { get; }
    /// <summary>ai-gateway 用量上报 ingest 令牌；BidCompare AiGateway:IngestToken 需一致</summary>
    public IResourceBuilder<ParameterResource> AiGatewayIngestToken { get; }
    /// <summary>LLM 配置 JSON（angineer-ai-inference LLM_CONFIGS）；本地运行由 ai-gateway 自动读仓库根 .env，仅发布容器注入</summary>
    public IResourceBuilder<ParameterResource> LlmConfigs { get; }

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

        MeetingBotKey = builder.AddParameter("meeting-bot-key", "dev-key", secret: true);
        AiGatewayApiToken = builder.AddParameter("ai-gateway-api-token", secret: true);
        AiGatewayIngestToken = builder.AddParameter("ai-gateway-ingest-token", secret: true);
        LlmConfigs = builder.AddParameter("llm-configs", secret: true);

    }
}
