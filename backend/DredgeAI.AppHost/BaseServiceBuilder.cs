using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker.Resources.ServiceNodes;

namespace DredgeAI.AppHost;

/// <summary>
/// Base 平台基础设施服务。
/// 本地运行：AddBaseService 注册服务（AddProject + 健康检查，端口由 Aspire 自动分配）
/// 发布模式：WithBasePublishEnvironment 注入环境变量 + Docker Compose 发布配置（含 Minio 配置）
/// </summary>
public static class BaseServiceBuilder
{
    /// <summary>
    /// 注册 Base 服务。
    /// </summary>
    /// <remarks>
    /// - WithHttpHealthCheck("/health")：Aspire 编排使用的健康检查端点
    /// HTTP 端口由 Aspire 自动分配；发布时 WithCommonBackendPublishEnvironment 固定为 8080。
    /// </remarks>
    public static IResourceBuilder<ProjectResource> AddBaseService(
        this IDistributedApplicationBuilder builder) =>
        builder.AddProject<Projects.DredgeAI_Base_Host>("base-service")
            .WithHttpHealthCheck("/health");

    /// <summary>
    /// 注入 Base 服务发布环境变量 + Docker Compose 发布配置。
    /// 公共配置（CORS + HTTP_PORTS）+ AuthServer（内部地址）+ 连接串 + 签名文件站点前缀 + Minio 对象存储 + expose + 健康检查块。
    /// </summary>
    /// <remarks>
    /// PublishAsDockerComposeService：自定义 healthcheck 块（Aspire 默认不生成）；
    /// 无宿主机端口映射（expose 8080）——仅 compose 网络内可达，外部 HTTP 流量经 gateway 按服务名路由。
    /// </remarks>
    public static IResourceBuilder<ProjectResource> WithBasePublishEnvironment(
        this IResourceBuilder<ProjectResource> baseSvc,
        ServiceParameters p) =>
        baseSvc.WithCommonBackendPublishEnvironment(p)
               .WithEnvironment("AuthServer__Authority", PublishCommonExtensions.AuthServerInternalAuthority)
               .WithEnvironment("App__FileWebSiteUrl", p.BaseFileWebSiteUrl)
               .WithEnvironment("Minio__EndPoint", p.MinioEndpoint)
               .WithEnvironment("Minio__BucketName", p.MinioBucket)
               .WithEnvironment("Minio__AccessKey", p.MinioAccessKey)
               .WithEnvironment("Minio__SecretKey", p.MinioSecretKey)
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
