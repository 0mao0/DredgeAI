using Aspire.Hosting.ApplicationModel;

namespace DredgeAI.AppHost;

/// <summary>
/// user-web / admin-web 前端。
/// 本地运行：AddViteApp 跑 vite dev（package.json 已有 "dev": "vite"）。isProxied: false ——
/// vite.config.ts 硬编码端口（5373/5374）且自带 /api 代理到 https://localhost:44361，Aspire 不再起代理。
/// 发布模式：AddDockerfile 用仓库内已有 Dockerfile 构建 nginx 静态托管镜像；
/// 构建上下文是仓库根（pnpm workspace + vendor submodule），contextPath = "../.."。
/// nginx.conf 只做静态托管（无 proxy_pass），API 流量由部署方边缘反代经 gateway 路由。
/// </summary>
public static class FrontendBuilder
{
    /// <summary>注册 user-web 前端（本地 vite dev :5373 / 发布 nginx 容器，宿主机端口经 ${USER_WEB_PORT} 占位符注入）。</summary>
    public static void AddUserWeb(this IDistributedApplicationBuilder builder)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            builder.AddDockerfile("user-web", "../..", "user-web/Dockerfile")
                .PublishAsDockerComposeService((_, service) =>
                {
                    service.Restart = "unless-stopped";
                    service.Ports.Clear();
                    service.Ports.Add("${USER_WEB_PORT}:80");
                });
        }
        else
        {
            builder.AddViteApp("user-web", "../../user-web")
                .WithPnpm()
                .WithHttpEndpoint(port: 5373, isProxied: false);
        }
    }

    /// <summary>注册 admin-web 前端（本地 vite dev :5374 / 发布 nginx 容器，宿主机端口经 ${ADMIN_WEB_PORT} 占位符注入）。</summary>
    public static void AddAdminWeb(this IDistributedApplicationBuilder builder)
    {
        if (builder.ExecutionContext.IsPublishMode)
        {
            builder.AddDockerfile("admin-web", "../..", "admin-web/Dockerfile")
                .PublishAsDockerComposeService((_, service) =>
                {
                    service.Restart = "unless-stopped";
                    service.Ports.Clear();
                    service.Ports.Add("${ADMIN_WEB_PORT}:80");
                });
        }
        else
        {
            builder.AddViteApp("admin-web", "../../admin-web")
                .WithPnpm()
                .WithHttpEndpoint(port: 5374, isProxied: false);
        }
    }
}
