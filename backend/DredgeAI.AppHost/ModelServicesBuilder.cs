using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes.Swarm;

namespace DredgeAI.AppHost;

/// <summary>
/// 5 个 AI 晨会模型服务（sensevoice/cosyvoice/insightface/yolo/meeting-bot）。
/// 所有端点（含 /health）都要求请求头 X-Meeting-Bot-Key（app 级 Depends(require_key)）。
/// 本地运行：AddUvicornApp 进程 + 固定端口；不挂 Aspire 健康检查（/health 要 key，401 会卡 WaitFor）。
/// 发布模式：AddDockerfile 使用各自仓库内已有 Dockerfile（cosyvoice 是 CUDA 镜像、insightface/yolo 有系统依赖，
/// Python 集成自动生成的 Dockerfile 不正确）；端口/环境变量/healthcheck/depends_on 对齐 services/meeting-bot/docker-compose.yml。
/// </summary>
public static class ModelServicesBuilder
{
    /// <summary>
    /// 注册 5 个模型服务并完成依赖接线。
    /// 本地：权重目录取 ModelServices:WeightsDir（appsettings.Development.json，默认 D:/AI/AImodles，
    /// 其他机器用 user-secrets 覆盖）；不注入 MEETING_BOT_KEY（各服务默认 dev-key，与 BidCompare 本地配置一致）。
    /// 发布：权重经 ${MODEL_WEIGHTS_DIR} 占位符 bind 挂载（部署方 .env 注入，对齐 ${GATEWAY_SERVICE_PORT} 模式）；
    /// MEETING_BOT_KEY 注入 p.MeetingBotKey；healthcheck 用 $${MEETING_BOT_KEY}（compose 转义 → 容器内 shell 展开）。
    /// </summary>
    public static void AddModelServices(this IDistributedApplicationBuilder builder, ServiceParameters p)
    {
        if (builder.ExecutionContext.IsPublishMode)
            builder.AddModelServicesPublish(p);
        else
            builder.AddModelServicesLocal();
    }

    // ============================================================================
    // 本地运行（AddUvicornApp 进程）
    // ============================================================================

    private static void AddModelServicesLocal(this IDistributedApplicationBuilder builder)
    {
        var weightsDir = builder.Configuration["ModelServices:WeightsDir"] ?? "D:/AI/AImodles";

        var sensevoice = builder.AddUvicornApp("sensevoice", "../../services/sensevoice", "app.main:app")
            .WithUv()
            .WithHttpEndpoint(port: 8102, env: "PORT")
            .WithEnvironment("MODEL_DIR", $"{weightsDir}/models")
            .WithEnvironment("ASR_DEVICE", "cpu");

        var insightface = builder.AddUvicornApp("insightface", "../../services/insightface", "app.main:app")
            .WithUv()
            .WithHttpEndpoint(port: 8103, env: "PORT")
            .WithEnvironment("MODEL_DIR", $"{weightsDir}/models")
            .WithEnvironment("FACE_PROVIDERS", "cpu")
            .WithEnvironment("FACE_RECOGNIZE_THRESHOLD", "0.55");

        var yolo = builder.AddUvicornApp("yolo", "../../services/yolo", "app.main:app")
            .WithUv()
            .WithHttpEndpoint(port: 8104, env: "PORT")
            .WithEnvironment("MODEL_DIR", $"{weightsDir}/models")
            .WithEnvironment("COUNT_DEVICE", "cpu");

        var cosyvoice = builder.AddUvicornApp("cosyvoice", "../../services/cosyvoice", "server:app")
            .WithUv()
            .WithHttpEndpoint(port: 8000, env: "PORT")
            .WithEnvironment("COSYVOICE_DATA", $"{weightsDir}/cosyvoice")
            .WithEnvironment("MODELSCOPE_CACHE", $"{weightsDir}/cosyvoice/modelscope")
            .WithEnvironment("TTS_VOICE_ID", "zh-male-news");

        // meeting-bot 是请求时转发上游，WaitFor 只等进程 Running（上游无 Aspire 健康检查），启动顺序容忍
        builder.AddUvicornApp("meeting-bot", "../../services/meeting-bot", "app.main:app")
            .WithUv()
            .WithHttpEndpoint(port: 8101, env: "PORT")
            .WithEnvironment("SENSEVOICE_URL", "http://localhost:8102")
            .WithEnvironment("COSYVOICE_URL", "http://localhost:8000")
            .WithEnvironment("INSIGHTFACE_URL", "http://localhost:8103")
            .WithEnvironment("YOLO_URL", "http://localhost:8104")
            .WithEnvironment("TTS_VOICE_ID", "zh-male-news")
            .WaitFor(sensevoice).WaitFor(cosyvoice).WaitFor(insightface).WaitFor(yolo);
    }

    // ============================================================================
    // 发布模式（AddDockerfile 容器，对齐 services/meeting-bot/docker-compose.yml）
    // ============================================================================

    private static void AddModelServicesPublish(this IDistributedApplicationBuilder builder, ServiceParameters p)
    {
        builder.AddDockerfile("sensevoice", "../../services/sensevoice")
            .WithEnvironment("MEETING_BOT_KEY", p.MeetingBotKey)
            .WithEnvironment("MODEL_DIR", "/app/models")
            .WithEnvironment("ASR_DEVICE", "cpu")
            .PublishAsDockerComposeService((_, service) =>
            {
                ConfigureModelService(service, "8102", "/health");
                service.Volumes.Add(new Volume { Name = "model-weights", Type = "bind", Source = "${MODEL_WEIGHTS_DIR}/models", Target = "/app/models", ReadOnly = true });
              
            });

        builder.AddDockerfile("insightface", "../../services/insightface")
            .WithEnvironment("MEETING_BOT_KEY", p.MeetingBotKey)
            .WithEnvironment("MODEL_DIR", "/app/models")
            .WithEnvironment("FACE_PROVIDERS", "cpu")
            .WithEnvironment("FACE_RECOGNIZE_THRESHOLD", "0.55")
            .PublishAsDockerComposeService((_, service) =>
            {
                ConfigureModelService(service, "8103", "/health");
                service.Volumes.Add(new Volume { Name = "model-weights", Type = "bind", Source = "${MODEL_WEIGHTS_DIR}/models", Target = "/app/models", ReadOnly = true });
            });

        builder.AddDockerfile("yolo", "../../services/yolo")
            .WithEnvironment("MEETING_BOT_KEY", p.MeetingBotKey)
            .WithEnvironment("MODEL_DIR", "/app/models")
            .WithEnvironment("COUNT_DEVICE", "cpu")
            .PublishAsDockerComposeService((_, service) =>
            {
                ConfigureModelService(service, "8104", "/health");
                service.Volumes.Add(new Volume { Name = "model-weights", Type = "bind", Source = "${MODEL_WEIGHTS_DIR}/models", Target = "/app/models", ReadOnly = true });
            });

        // cosyvoice GPU：Aspire.Hosting.Docker 13.x 的 compose 类型模型不支持 device reservations
        //（Swarm.ResourceSpec 只有 Cpus/Memory，Service.Devices 是 legacy 短语法，不可靠触发 nvidia runtime），
        // 发布产物不带 GPU 配置。部署方在生成后的 docker-compose.yaml 中手工为 cosyvoice 补充：
        //   deploy:
        //     resources:
        //       reservations:
        //         devices:
        //           - driver: nvidia
        //             count: all
        //             capabilities: [gpu]
        builder.AddDockerfile("cosyvoice", "../../services/cosyvoice")
            .WithEnvironment("MEETING_BOT_KEY", p.MeetingBotKey)
            .WithEnvironment("COSYVOICE_DATA", "/data")
            .WithEnvironment("MODELSCOPE_CACHE", "/data/modelscope")
            .WithEnvironment("TTS_VOICE_ID", "zh-male-news")
            .PublishAsDockerComposeService((_, service) =>
            {
                // 健康端点是 /api/health 且需 model_loaded:true；模型加载慢，重试/起始宽限期对齐现有 compose
                service.Restart = "unless-stopped";
                service.Ports.Clear();
                service.Expose.Clear();
                service.Expose.Add("8000");
                service.Healthcheck = new Healthcheck
                {
                    Test = ["CMD-SHELL", "curl -fsS -H \"X-Meeting-Bot-Key: $${MEETING_BOT_KEY}\" http://localhost:8000/api/health | grep -q '\"model_loaded\":true'"],
                    Interval = "10s",
                    Timeout = "5s",
                    Retries = 30,
                    StartPeriod = "120s"
                };
                // 读写挂载：MODELSCOPE_CACHE 会写入
                service.Volumes.Add(new Volume { Name = "cosyvoice-data", Type = "bind", Source = "${MODEL_WEIGHTS_DIR}/cosyvoice", Target = "/data" });
                
                
            });

        // meeting-bot 上游 URL 用 compose 服务名（与其 settings 默认值一致，显式注入保持清晰）
        builder.AddDockerfile("meeting-bot", "../../services/meeting-bot")
            .WithEnvironment("MEETING_BOT_KEY", p.MeetingBotKey)
            .WithEnvironment("SENSEVOICE_URL", "http://sensevoice:8102")
            .WithEnvironment("COSYVOICE_URL", "http://cosyvoice:8000")
            .WithEnvironment("INSIGHTFACE_URL", "http://insightface:8103")
            .WithEnvironment("YOLO_URL", "http://yolo:8104")
            .WithEnvironment("TTS_VOICE_ID", "zh-male-news")
            .PublishAsDockerComposeService((_, service) =>
            {
                ConfigureModelService(service, "8101", "/health");
                service.DependsOn["sensevoice"] = new ServiceDependency { Condition = "service_healthy" };
                service.DependsOn["cosyvoice"] = new ServiceDependency { Condition = "service_healthy" };
                service.DependsOn["insightface"] = new ServiceDependency { Condition = "service_healthy" };
                service.DependsOn["yolo"] = new ServiceDependency { Condition = "service_healthy" };
            });
    }

    /// <summary>
    /// 模型服务 compose 公共配置：restart + 仅 expose 容器端口（无宿主机端口，与后端服务一致）+ 带 key 的健康检查。
    /// $${MEETING_BOT_KEY} 是 compose 转义 → 容器内 shell 展开环境变量；interval/timeout/retries/start_period 对齐现有 compose。
    /// </summary>
    private static void ConfigureModelService(Service service, string port, string healthPath)
    {
        service.Restart = "unless-stopped";
        service.Ports.Clear();
        service.Expose.Clear();
        service.Expose.Add(port);
        service.Healthcheck = new Healthcheck
        {
            Test = ["CMD-SHELL", $"curl -fsS -H \"X-Meeting-Bot-Key: $${{MEETING_BOT_KEY}}\" http://localhost:{port}{healthPath} || exit 1"],
            Interval = "10s",
            Timeout = "5s",
            Retries = 12,
            StartPeriod = "30s"
        };
    }
}
