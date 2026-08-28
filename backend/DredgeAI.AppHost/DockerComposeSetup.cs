using Aspire.Hosting.Docker;

namespace DredgeAI.AppHost;

/// <summary>
/// Docker Compose 发布环境配置（公共）。
/// 发布目标：Docker Compose（惰性激活，本地 dotnet run 时无副作用）。
/// </summary>
public static class DockerComposeSetup
{
    /// <summary>
    /// 添加 Docker Compose 发布环境，并配置自定义网络名与 Dashboard。
    /// </summary>
    /// <remarks>
    /// - 自定义网络名（默认 "aspire"），影响所有服务的 networks 列表和顶层 networks 定义
    /// - 固定 Dashboard 宿主机端口（默认仅容器端口，宿主机端口由 Docker 随机分配）
    /// - 认证：默认 BrowserToken 模式，未指定 token 时 Dashboard 每次启动随机生成（需查日志获取）
    ///   设置 Dashboard__Frontend__BrowserToken 后，登录页输入此固定 token 即可访问
    /// </remarks>
    public static IResourceBuilder<DockerComposeEnvironmentResource> AddDockerComposeEnvironment(
        this IDistributedApplicationBuilder builder,
        ServiceParameters parameters,
        bool withDashboard = true)
    {
        var composeEnv = builder.AddDockerComposeEnvironment("docker-compose");
        composeEnv.Resource.DefaultNetworkName = "dredge-ai-net";

        if (withDashboard)
        {
            composeEnv.WithDashboard(dashboard =>
            {
                dashboard.WithContainerName("dashboard");
                dashboard.WithHostPort(51329);
                dashboard.WithEnvironment("Dashboard__Frontend__BrowserToken", parameters.DashboardBrowserToken);
            });
        }
        else
        {
            // Dashboard 默认启用，须显式关闭
            composeEnv.WithDashboard(enabled: false);
        }

        return composeEnv;
    }
}
