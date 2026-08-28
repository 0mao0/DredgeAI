// DredgeAI 后端服务体系 — Aspire 编排主机

using DredgeAI.AppHost;

// 文件组织：
//   AppHost.cs                  — 入口，按本地/发布模式编排各服务
//   OrchestrationTier.cs        — 本地运行分层开关（backend/python/frontend）
//   ServiceParameters.cs        — 公共：所有发布参数定义（.env 注入）
//   DockerComposeSetup.cs       — 公共：Docker Compose 环境 + Dashboard
//   PublishCommonExtensions.cs  — 公共：后端服务发布环境变量（CORS + HTTP_PORTS）+ compose 服务节点助手（EnableComposeReplicas）
//   AuthServiceBuilder.cs       — Auth 模块（服务定义 + 发布环境变量）
//   BaseServiceBuilder.cs       — Base 模块
//   BidCompareServiceBuilder.cs — BidCompare 模块
//   GatewayServiceBuilder.cs    — Gateway 网关模块（YARP 统一入口）
//   PythonServicesBuilder.cs    — compare-algo + ai-gateway（uvicorn 进程 / Dockerfile 容器）
//   ModelServicesBuilder.cs     — 5 个 AI 晨会模型服务（sensevoice/cosyvoice/insightface/yolo/meeting-bot）
//   FrontendBuilder.cs          — user-web / admin-web（vite dev / nginx 容器）
//
// 本地运行（dotnet run）：
//   - 分级启动：--launch-profile python|frontend|all，或 dotnet run -- --tier=backend,python（逗号组合）；
//     缺省 = backend（仅 4 个 .NET 后端，与改造前行为一致）
//   - Docker Compose 环境惰性激活，无副作用
//   - 各服务读自己的 appsettings.json，发布参数不触发值检查
//
// 发布模式（aspire publish --output ./aspire-publish）：
//   - 缺省注册全部资源，-- --tier=backend|python|frontend 可只发布对应层（publish.ps1/publish.sh 已封装）
//   - 缺省含 docker-compose-dashboard，-- --dashboard=false 可去掉（publish.ps1/publish.sh 的 -NoDashboard/--no-dashboard）
//   - 生成 docker-compose.yaml，所有服务通过环境变量注入配置
//   - 容器运行时环境变量按 .NET 配置优先级覆盖 appsettings.json

var builder = DistributedApplication.CreateBuilder(args);

var isPublish = builder.ExecutionContext.IsPublishMode;
var tier = OrchestrationTierResolver.Resolve(builder, isPublish ? OrchestrationTier.All : OrchestrationTier.Backend); // 发布模式缺省全量，可用 --tier 收窄
var parameters = new ServiceParameters(builder);       // 两种模式都构造；未引用的参数本地不弹提示

if (isPublish)
{
    // 公共：Docker Compose 发布环境（任何子集发布都需要）；-- --dashboard=false 可去掉 Dashboard
    var withDashboard = !string.Equals(builder.Configuration["dashboard"], "false", StringComparison.OrdinalIgnoreCase);
    builder.AddDockerComposeEnvironment(parameters, withDashboard);
}

// ============================================================================
// .NET 后端（Auth 根；BidCompare 依赖 Auth；Gateway 依赖 Auth+Base+BidCompare）
// AddXxxService：注册项目 + 健康检查（本地端口由 Aspire 自动分配）
// WithReference：注入上游服务的发现端点；WaitFor：下游等待上游健康检查通过后才启动
// 发布模式：WithXxxPublishEnvironment 注入环境变量（CORS/HTTP_PORTS/连接串/内部 AuthServer 地址等）
//   + PublishAsDockerComposeService（auth/base/bidcompare expose 无宿主机端口；gateway 保留端口映射）
// ============================================================================
if (tier.HasFlag(OrchestrationTier.Backend))
{
    var auth = builder.AddAuthService();
    var baseSvc = builder.AddBaseService();
    var bidCompare = builder.AddBidCompareService();
    var gateway = builder.AddGatewayService();

    // Auth 是根服务，无上游依赖
    // BidCompare 依赖 Auth（JWT 令牌验证）
    bidCompare.WithReference(auth).WaitFor(auth);
    // Gateway 依赖 Auth + Base + BidCompare（YARP 路由目标全部就绪后才启动）
    gateway.WithReference(auth).WithReference(baseSvc).WithReference(bidCompare).WaitFor(auth).WaitFor(baseSvc).WaitFor(bidCompare);

    if (isPublish)
    {
        auth.WithAuthPublishEnvironment(parameters);
        baseSvc.WithBasePublishEnvironment(parameters);
        bidCompare.WithBidComparePublishEnvironment(parameters);
        gateway.WithGatewayPublishEnvironment(parameters);
    }
}

// ============================================================================
// Python 服务（本地 AddUvicornApp 进程 / 发布 AddDockerfile 容器，见各 Builder 注释）
// ============================================================================
if (tier.HasFlag(OrchestrationTier.Python))
{
    builder.AddCompareAlgoService(parameters);
    builder.AddAiGatewayService(parameters);
    builder.AddModelServices(parameters);
}

// ============================================================================
// 前端（本地 AddViteApp vite dev / 发布 AddDockerfile nginx 容器）
// ============================================================================
if (tier.HasFlag(OrchestrationTier.Frontend))
{
    builder.AddUserWeb();
    builder.AddAdminWeb();
}

builder.Build().Run();
