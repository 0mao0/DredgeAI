using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes.Swarm;

namespace DredgeAI.AppHost;

/// <summary>
/// 后端服务发布环境变量公共配置（CORS + HTTP_PORTS）+ compose 服务节点公共助手（EnableComposeReplicas）+ 内部 AuthServer 地址常量。
/// 本地 dotnet run 时各服务读自己的 appsettings.json，零影响。
/// 容器运行时环境变量按 .NET 配置优先级覆盖 appsettings.json。
/// </summary>
public static class PublishCommonExtensions
{
    /// <summary>
    /// compose 网络内 Auth 服务的固定内部地址，作为各后端容器 AuthServer:Authority（JWT 元数据引导）。
    /// 容器按服务名直连，不经公网域名回环；令牌 issuer 由 OpenIddict:IssuerUri（OPENIDDICT_ISSUER_URI）
    /// 独立固定为公网入口，与该内部地址解耦。
    /// </summary>
    public const string AuthServerInternalAuthority = "http://auth-service:8080";

    /// <summary>
    /// 注入公共环境变量：CORS 源列表 + HTTP_PORTS（固定 8080）。
    /// </summary>
    /// <remarks>
    /// HTTP_PORTS 必须显式设置为 8080，确保容器监听固定端口，与各 Host 项目的 Dockerfile (EXPOSE 8080 / ASPNETCORE_URLS) 一致。
    /// Aspire 生成 HTTP_PORTS=8080 会覆盖 Dockerfile 的 ASPNETCORE_URLS，但值相同，行为一致。
    /// </remarks>
    public static IResourceBuilder<ProjectResource> WithCommonBackendPublishEnvironment(
        this IResourceBuilder<ProjectResource> svc,
        ServiceParameters p) =>
        svc.WithEnvironment("App__CorsOrigins", p.CorsOrigins)
            .WithEnvironment("HTTP_PORTS", "8080");

    /// <summary>
    /// 使服务在 Docker Compose 中支持多副本运行：
    /// 清空宿主机端口映射（多副本共用同一宿主机端口必然冲突；调用方经 compose 网络内的服务名访问，
    /// Docker 内嵌 DNS 将服务名轮询解析到所有副本 IP），并写入 deploy.replicas。
    /// 用法：任一服务 Builder 的 PublishAsDockerComposeService 回调中调用 service.EnableComposeReplicas(2)
    ///（若回调中仍有固定宿主机端口映射 service.Ports.Add(...)，调用即可覆盖清除），然后 ./publish.sh 重新生成。
    /// 4 个业务后端（auth/base/business/iot-http）已 expose 化、无宿主机端口，加一行即可多副本。
    /// </summary>
    /// <remarks>
    /// 仅适用于无状态 HTTP 服务。Iot ConsoleHost 不适用：TCP 固定端口映射与共享 WAL bind 卷在多副本下均冲突，
    /// 需专门的 L4 负载均衡 + 每副本独立卷设计。
    /// compose 的 deploy.replicas 与 docker compose up --scale 互斥（同时使用会报错）——
    /// 调整副本数 = 改 replicas 参数重新 publish，不要用 --scale。
    /// 多副本服务不得设置 container_name（副本容器名必须唯一；当前仅 dashboard 设置了，不受影响）。
    /// </remarks>
    public static void EnableComposeReplicas(this Service service, int replicas)
    {
        service.Ports.Clear();
        service.Deploy = new Deploy { Replicas = replicas };
    }
}
