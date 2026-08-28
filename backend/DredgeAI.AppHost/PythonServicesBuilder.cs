using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker.Resources.ServiceNodes;

namespace DredgeAI.AppHost;

/// <summary>
/// compare-algo（确定性比标算法，8100）+ ai-gateway（平台唯一 LLM 入口，8200）。
/// 两个服务的 /healthz 均无鉴权，本地可直接挂 Aspire 健康检查。
/// 本地运行：AddUvicornApp 进程 + uv 依赖管理 + 固定端口（与 BidCompare 本地 appsettings 的 localhost 配置一致）。
/// 发布模式：AddDockerfile 使用各自仓库内已有 Dockerfile（不用 Python 集成自动生成——ai-gateway 消费 angineer-ai-inference），
/// compose 内仅 expose 容器端口，无宿主机端口映射，外部流量经 gateway 按服务名路由。
/// </summary>
public static class PythonServicesBuilder
{
    /// <summary>compose 网络内 BidCompare 用量上报地址（覆盖 ai-gateway settings 默认的 localhost:44361）</summary>
    private const string AiGatewayUsageReportUrl = "http://bidcompare-service:8080/api/ai-gateway/usage-records";

    /// <summary>
    /// 注册 compare-algo 服务。settings 全默认值（前缀 COMPARE_ALGO_），两种模式均无需环境变量。
    /// </summary>
    public static void AddCompareAlgoService(this IDistributedApplicationBuilder builder, ServiceParameters p)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            builder.AddDockerfile("compare-algo", "../../services/compare-algo")
                .PublishAsDockerComposeService((_, service) =>
                {
                    service.Restart = "unless-stopped";
                    service.Ports.Clear();
                    service.Expose.Clear();
                    service.Expose.Add("8100");
                    service.Healthcheck = CreateHealthzHealthcheck(8100);
                });
        }
        else
        {
            builder.AddUvicornApp("compare-algo", "../../services/compare-algo", "app.main:app")
                .WithUv()
                .WithHttpEndpoint(port: 8100, env: "PORT")
                .WithHttpHealthCheck("/healthz");
        }
    }

    /// <summary>
    /// 注册 ai-gateway 服务。
    /// 本地分支不注入任何环境变量：load_dotenv 自动读仓库根 .env 的 LLM_CONFIGS（路径由文件位置推导，与 cwd 无关），
    /// token 默认空 = 鉴权关闭，与 BidCompare 本地 appsettings（AiGateway:ApiToken/IngestToken 空）一致。
    /// </summary>
    public static void AddAiGatewayService(this IDistributedApplicationBuilder builder, ServiceParameters p)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            // 发布容器无仓库根 .env 文件，LLM_CONFIGS 必须显式注入（settings 前缀 AI_GATEWAY_）
            builder.AddDockerfile("ai-gateway", "../../services/ai-gateway")
                .WithEnvironment("AI_GATEWAY_API_TOKEN", p.AiGatewayApiToken)
                .WithEnvironment("AI_GATEWAY_INGEST_TOKEN", p.AiGatewayIngestToken)
                .WithEnvironment("LLM_CONFIGS", p.LlmConfigs)
                .WithEnvironment("AI_GATEWAY_USAGE_REPORT_URL", AiGatewayUsageReportUrl)
                .PublishAsDockerComposeService((_, service) =>
                {
                    service.Restart = "unless-stopped";
                    service.Ports.Clear();
                    service.Expose.Clear();
                    service.Expose.Add("8200");
                    service.Healthcheck = CreateHealthzHealthcheck(8200);
                });
        }
        else
        {
            builder.AddUvicornApp("ai-gateway", "../../services/ai-gateway", "app.main:app")
                .WithUv()
                .WithHttpEndpoint(port: 8200, env: "PORT")
                .WithHttpHealthCheck("/healthz");
        }
    }

    /// <summary>镜像已含 curl（已核实），健康检查直接打 /healthz（无鉴权）。</summary>
    private static Healthcheck CreateHealthzHealthcheck(int port) => new()
    {
        Test = ["CMD-SHELL", $"curl -f http://localhost:{port}/healthz || exit 1"],
        Interval = "30s",
        Timeout = "5s",
        Retries = 3,
        StartPeriod = "40s"
    };
}
