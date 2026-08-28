using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker.Resources.ServiceNodes;

namespace DredgeAI.AppHost;

/// <summary>
/// BidCompare 业务服务。
/// 本地运行：AddBidCompareService 注册服务（AddProject + 健康检查，端口由 Aspire 自动分配）
/// 发布模式：WithBidComparePublishEnvironment 注入环境变量 + Docker Compose 发布配置（无 Minio 参数——BidCompare 用自有 Storage:S3 配置节）
/// </summary>
public static class BidCompareServiceBuilder
{
    /// <summary>
    /// 注册 BidCompare 服务。
    /// </summary>
    /// <remarks>
    /// - WithHttpHealthCheck("/health")：Aspire 编排使用的健康检查端点
    /// HTTP 端口由 Aspire 自动分配；发布时 WithCommonBackendPublishEnvironment 固定为 8080。
    /// </remarks>
    public static IResourceBuilder<ProjectResource> AddBidCompareService(
        this IDistributedApplicationBuilder builder) =>
        builder.AddProject<Projects.DredgeAI_BidCompare_Host>("bidcompare-service")
            .WithHttpHealthCheck("/health");

    /// <summary>
    /// 注入 BidCompare 服务发布环境变量 + Docker Compose 发布配置。
    /// 公共配置（CORS + HTTP_PORTS）+ AuthServer（内部地址）+ expose + 健康检查块。
    /// </summary>
    /// <remarks>
    /// PublishAsDockerComposeService：自定义 healthcheck 块（Aspire 默认不生成）；
    /// 无宿主机端口映射（expose 8080）——仅 compose 网络内可达，外部 HTTP 流量经 gateway 按服务名路由。
    /// </remarks>
    public static IResourceBuilder<ProjectResource> WithBidComparePublishEnvironment(
        this IResourceBuilder<ProjectResource> bidCompareSvc,
        ServiceParameters p) =>
        bidCompareSvc.WithCommonBackendPublishEnvironment(p)
               .WithEnvironment("AuthServer__Authority", PublishCommonExtensions.AuthServerInternalAuthority)
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
