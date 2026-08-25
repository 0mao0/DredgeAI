using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker.Resources.ServiceNodes;

namespace DredgeAI.AppHost;

/// <summary>
/// Auth 身份认证服务（OpenIddict + Identity）。
/// 本地运行：AddAuthService 注册服务（AddProject + 健康检查，端口由 Aspire 自动分配）
/// 发布模式：WithAuthPublishEnvironment 注入环境变量 + Docker Compose 发布配置
/// </summary>
public static class AuthServiceBuilder
{
    /// <summary>
    /// 注册 Auth 服务。
    /// </summary>
    /// <remarks>
    /// - WithHttpHealthCheck("/health")：Aspire 编排使用的健康检查端点
    /// HTTP 端口由 Aspire 自动分配；发布时 WithCommonBackendPublishEnvironment 固定为 8080。
    /// </remarks>
    public static IResourceBuilder<ProjectResource> AddAuthService(
        this IDistributedApplicationBuilder builder) =>
        builder.AddProject<Projects.DredgeAI_Auth_Host>("auth-service")
            .WithHttpHealthCheck("/health");

    /// <summary>
    /// 注入 Auth 服务发布环境变量 + Docker Compose 发布配置。
    /// 公共配置（CORS + HTTP_PORTS）+ 连接串 + OpenIddict Issuer/客户端 RootUrl + expose + 健康检查块。
    /// </summary>
    /// <remarks>
    /// 其他服务的 AuthServer:Authority 注入 compose 内部固定地址（PublishCommonExtensions.AuthServerInternalAuthority），与本服务无关。
    /// OpenIddict 客户端 RootUrl 指向前端 Base.Host 的对外地址。
    /// PublishAsDockerComposeService：自定义 healthcheck 块（Aspire 默认不生成）；
    /// 无宿主机端口映射（expose 8080）——仅 compose 网络内可达，外部 HTTP 流量经 gateway 按服务名路由。
    /// </remarks>
    public static IResourceBuilder<ProjectResource> WithAuthPublishEnvironment(
        this IResourceBuilder<ProjectResource> auth,
        ServiceParameters p) =>
        auth.WithCommonBackendPublishEnvironment(p)
            .WithEnvironment("OpenIddict__IssuerUri", p.OpenIddictIssuerUri)
            .WithEnvironment("OpenIddict__Applications__DredgeAI_Web__RootUrl", p.OpenIddictRootUrl)
            .WithEnvironment("OpenIddict__Applications__DredgeAI_App__RootUrl", p.OpenIddictRootUrl)
            .WithEnvironment("OpenIddict__Applications__DredgeAI_Swagger__RootUrl", p.OpenIddictRootUrl)
            .PublishAsDockerComposeService((_, service) =>
            {
                service.Restart = "unless-stopped";
                service.Expose.Clear();
                service.Expose.Add("8080");
                service.Healthcheck = new Healthcheck
                {
                    Test = ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"],
                    Interval = "30s",
                    Timeout = "5s",
                    Retries = 3,
                    StartPeriod = "40s"
                };
            });
}
