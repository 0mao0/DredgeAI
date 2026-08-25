// DredgeAI 后端服务体系 — Aspire 编排主机

using DredgeAI.AppHost;

// 文件组织：
//   AppHost.cs                  — 入口，按本地/发布模式编排各服务
//   ServiceParameters.cs        — 公共：所有发布参数定义（.env 注入）
//   DockerComposeSetup.cs       — 公共：Docker Compose 环境 + Dashboard
//   PublishCommonExtensions.cs  — 公共：后端服务发布环境变量（CORS + HTTP_PORTS）+ compose 服务节点助手（EnableComposeReplicas）
//   AuthServiceBuilder.cs       — Auth 模块（服务定义 + 发布环境变量）
//   BaseServiceBuilder.cs       — Base 模块
//   GatewayServiceBuilder.cs    — Gateway 网关模块（YARP 统一入口）
//
// 本地运行（dotnet run）：
//   - Docker Compose 环境惰性激活，无副作用
//   - 各服务读自己的 appsettings.json，发布参数不触发值检查
//
// 发布模式（aspire publish --output ./aspire-publish）：
//   - 生成 docker-compose.yaml，所有服务通过环境变量注入配置
//   - 容器运行时环境变量按 .NET 配置优先级覆盖 appsettings.json

var builder = DistributedApplication.CreateBuilder(args);

// ============================================================================
// 后端服务定义（本地运行 + 发布通用）
// AddXxxService：注册项目 + 健康检查（本地端口由 Aspire 自动分配）
// ============================================================================
var auth = builder.AddAuthService();
var baseSvc = builder.AddBaseService();
var gateway = builder.AddGatewayService();

// ============================================================================
// 服务依赖关系
// WithReference：注入上游服务的发现端点（Dashboard 依赖图 + 服务发现配置）
// WaitFor：启动顺序 — 下游服务等待上游健康检查通过后才启动
// ============================================================================
// Auth 是根服务，无上游依赖
// Base 依赖 Auth（JWT 元数据 + 令牌验证）
baseSvc.WithReference(auth).WaitFor(auth);
// Gateway 依赖 Auth + Base（YARP 路由目标全部就绪后才启动）
gateway.WithReference(auth).WithReference(baseSvc).WaitFor(auth).WaitFor(baseSvc);

// ============================================================================
// 发布模式：后端环境变量注入 + Docker Compose 发布配置
// 本地 dotnet run 时此块不执行，各服务读自己的 appsettings.json，零影响
// WithXxxPublishEnvironment：注入环境变量（CORS/HTTP_PORTS/连接串/内部 AuthServer 地址等）
//   + PublishAsDockerComposeService（auth/base expose 无宿主机端口；gateway 保留端口映射；统一健康检查块）
// ============================================================================
if (builder.ExecutionContext.IsPublishMode)
{
    // 公共：Docker Compose 发布环境 + Dashboard（惰性激活，本地运行无副作用）
    var parameters = new ServiceParameters(builder);
    builder.AddDockerComposeEnvironment(parameters);

    // 各后端服务发布环境变量（公共 CORS/HTTP_PORTS + 服务独立配置 + Docker Compose 发布配置）
    auth.WithAuthPublishEnvironment(parameters);
    baseSvc.WithBasePublishEnvironment(parameters);
    gateway.WithGatewayPublishEnvironment(parameters);
}

builder.Build().Run();
