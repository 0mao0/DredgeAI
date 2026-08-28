# 构建上下文：backend/
# 构建命令：docker build -f BidCompare/src/DredgeAI.BidCompare.Host/Dockerfile backend/
# 说明：BidCompare 比标业务服务（ABP + PostgreSQL + Minio/S3 + ai-gateway/meeting-bot/compare-algo 编排）

# ============================================================================
# 阶段 1：构建
# ============================================================================
FROM mcr.azure.cn/dotnet/sdk:10.0 AS build
WORKDIR /src

# 复制 NuGet 配置与共享 MSBuild 属性（私有源凭证在 nuget.config 中）
COPY nuget.config Directory.Packages.props common.props ./

# 复制所有 csproj 用于 restore 缓存（仅 csproj 变化时才重新 restore）
# 覆盖 DredgeAI.BidCompare.Host.csproj 的 ProjectReference 传递闭包：
#   Host → Host.Shared / ServiceDefaults / Application / EntityFrameworkCore / HttpApi
#   Application → Domain / Application.Contracts；EntityFrameworkCore → Domain
#   HttpApi → Application.Contracts；Application.Contracts/Domain → Domain.Shared
COPY ["DredgeAI.Host.Shared/DredgeAI.Host.Shared.csproj", "DredgeAI.Host.Shared/"]
COPY ["DredgeAI.ServiceDefaults/DredgeAI.ServiceDefaults.csproj", "DredgeAI.ServiceDefaults/"]
COPY ["BidCompare/src/DredgeAI.BidCompare.Domain.Shared/DredgeAI.BidCompare.Domain.Shared.csproj", "BidCompare/src/DredgeAI.BidCompare.Domain.Shared/"]
COPY ["BidCompare/src/DredgeAI.BidCompare.Domain/DredgeAI.BidCompare.Domain.csproj", "BidCompare/src/DredgeAI.BidCompare.Domain/"]
COPY ["BidCompare/src/DredgeAI.BidCompare.Application.Contracts/DredgeAI.BidCompare.Application.Contracts.csproj", "BidCompare/src/DredgeAI.BidCompare.Application.Contracts/"]
COPY ["BidCompare/src/DredgeAI.BidCompare.Application/DredgeAI.BidCompare.Application.csproj", "BidCompare/src/DredgeAI.BidCompare.Application/"]
COPY ["BidCompare/src/DredgeAI.BidCompare.EntityFrameworkCore/DredgeAI.BidCompare.EntityFrameworkCore.csproj", "BidCompare/src/DredgeAI.BidCompare.EntityFrameworkCore/"]
COPY ["BidCompare/src/DredgeAI.BidCompare.HttpApi/DredgeAI.BidCompare.HttpApi.csproj", "BidCompare/src/DredgeAI.BidCompare.HttpApi/"]
COPY ["BidCompare/src/DredgeAI.BidCompare.Host/DredgeAI.BidCompare.Host.csproj", "BidCompare/src/DredgeAI.BidCompare.Host/"]
RUN dotnet restore "BidCompare/src/DredgeAI.BidCompare.Host/DredgeAI.BidCompare.Host.csproj"

# 复制全部源码（.dockerignore 已排除 bin/obj/Logs）
COPY . .

# 编译发布
RUN dotnet publish "BidCompare/src/DredgeAI.BidCompare.Host/DredgeAI.BidCompare.Host.csproj" \
    -c Release -o /app/publish --no-restore

# ============================================================================
# 阶段 2：运行时
# ============================================================================
FROM mcr.azure.cn/dotnet/aspnet:10.0
WORKDIR /app

# 切换为国内源（阿里云）并安装 curl（健康检查依赖）、libgssapi-krb5-2
# （Npgsql 启动时探测 Kerberos/GSSAPI 原生库，缺失会报 Cannot load library libgssapi_krb5.so.2）
# 放在 COPY --from=build 之前，只依赖基础镜像，避免每次代码变化都重装
RUN sed -i 's|archive.ubuntu.com|mirrors.aliyun.com|g; s|security.ubuntu.com|mirrors.aliyun.com|g' /etc/apt/sources.list.d/ubuntu.sources 2>/dev/null || \
    sed -i 's|archive.ubuntu.com|mirrors.aliyun.com|g; s|security.ubuntu.com|mirrors.aliyun.com|g' /etc/apt/sources.list && \
    apt-get update && \
    apt-get install -y --no-install-recommends curl libgssapi-krb5-2  locales locales-all && \
    rm -rf /var/lib/apt/lists/*
ENV LANG=zh_CN.UTF-8

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

ENTRYPOINT ["dotnet", "DredgeAI.BidCompare.Host.dll"]
