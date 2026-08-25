using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker.Resources.ServiceNodes;

namespace DredgeAI.AppHost;

/// <summary>
/// Gateway 网关服务（YARP 反向代理 + 请求限流 + JWT 验证，统一入口）。
/// 本地运行：AddGatewayService 注册服务（AddProject + 健康检查 + 外部端点标记，端口由 Aspire 自动分配）
/// 发布模式：WithGatewayPublishEnvironment 注入环境变量 + Docker Compose 发布配置
/// </summary>
public static class GatewayServiceBuilder
{
    /// <summary>
    /// 注册 Gateway 服务。健康检查 + 外部端点（唯一对外 HTTP 入口）。
    /// </summary>
    public static IResourceBuilder<ProjectResource> AddGatewayService(
        this IDistributedApplicationBuilder builder) =>
        builder.AddProject<Projects.DredgeAI_Gateway_Host>("gateway-service")
            .WithHttpHealthCheck("/health")
            .WithExternalHttpEndpoints();

    /// <summary>
    /// 注入 Gateway 发布环境变量 + Docker Compose 发布配置。
    /// 公共配置（CORS + HTTP_PORTS）+ AuthServer（内部地址）+ YARP 集群目标改写为 compose 内部服务名 + 端口映射 + 健康检查块。
    /// </summary>
    /// <remarks>
    /// compose 网络（dredge-ai-net）内按 Aspire 资源名互访，容器内均监听 8080；
    /// ReverseProxy__Clusters__* 环境变量按 .NET 配置优先级覆盖 appsettings.json 中的 localhost 目标。
    /// </remarks>
    public static IResourceBuilder<ProjectResource> WithGatewayPublishEnvironment(
        this IResourceBuilder<ProjectResource> gateway,
        ServiceParameters p) =>
        gateway.WithCommonBackendPublishEnvironment(p)
               .WithEnvironment("AuthServer__Authority", PublishCommonExtensions.AuthServerInternalAuthority)
               .PublishAsDockerComposeService((_, service) =>
               {
                   service.Restart = "unless-stopped";
                   service.Ports.Clear();
                   service.Ports.Add("${GATEWAY_SERVICE_PORT}:8080");
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
