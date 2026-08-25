# 构建上下文：DredgeAI/
# 构建命令：docker build -f Gateway/DredgeAI.Gateway.Host/Dockerfile DredgeAI/
# 说明：Gateway 网关服务（YARP 反向代理 + 请求限流 + JWT 验证，统一入口）

# ============================================================================
# 阶段 1：构建
# ============================================================================
FROM mcr.azure.cn/dotnet/sdk:10.0 AS build
WORKDIR /src

# 复制 NuGet 配置与共享 MSBuild 属性（私有源凭证在 nuget.config 中）
COPY nuget.config Directory.Packages.props common.props ./

# 复制所有 csproj 用于 restore 缓存（仅 csproj 变化时才重新 restore）
COPY ["DredgeAI.Host.Shared/DredgeAI.Host.Shared.csproj", "DredgeAI.Host.Shared/"]
COPY ["DredgeAI.ServiceDefaults/DredgeAI.ServiceDefaults.csproj", "DredgeAI.ServiceDefaults/"]
COPY ["Gateway/DredgeAI.Gateway.Host/DredgeAI.Gateway.Host.csproj", "Gateway/DredgeAI.Gateway.Host/"]

RUN dotnet restore "Gateway/DredgeAI.Gateway.Host/DredgeAI.Gateway.Host.csproj"

# 复制全部源码（.dockerignore 已排除 bin/obj/Logs）
COPY . .

# 编译发布
RUN dotnet publish "Gateway/DredgeAI.Gateway.Host/DredgeAI.Gateway.Host.csproj" \
    -c Release -o /app/publish --no-restore

# ============================================================================
# 阶段 2：运行时
# ============================================================================
FROM mcr.azure.cn/dotnet/aspnet:10.0
WORKDIR /app

# 切换为国内源（阿里云）并安装 curl（健康检查依赖）
# 放在 COPY --from=build 之前，只依赖基础镜像，避免每次代码变化都重装
RUN sed -i 's|archive.ubuntu.com|mirrors.aliyun.com|g; s|security.ubuntu.com|mirrors.aliyun.com|g' /etc/apt/sources.list.d/ubuntu.sources 2>/dev/null || \
    sed -i 's|archive.ubuntu.com|mirrors.aliyun.com|g; s|security.ubuntu.com|mirrors.aliyun.com|g' /etc/apt/sources.list && \
    apt-get update && \
    apt-get install -y --no-install-recommends curl  locales locales-all && \
    rm -rf /var/lib/apt/lists/*
ENV LANG=zh_CN.UTF-8

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "DredgeAI.Gateway.Host.dll"]
