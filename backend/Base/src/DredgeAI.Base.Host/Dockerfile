# 构建上下文：DredgeAI/
# 构建命令：docker build -f Base/src/DredgeAI.Base.Host/Dockerfile DredgeAI/
# 说明：Base 服务（平台基础设施），含审计日志/安全日志/字典管理/菜单管理/文件管理（Minio）

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
COPY ["Core/src/DredgeAI.Core.Domain.Shared/DredgeAI.Core.Domain.Shared.csproj", "Core/src/DredgeAI.Core.Domain.Shared/"]
COPY ["Base/src/DredgeAI.Base.Domain.Shared/DredgeAI.Base.Domain.Shared.csproj", "Base/src/DredgeAI.Base.Domain.Shared/"]
COPY ["Base/src/DredgeAI.Base.Domain/DredgeAI.Base.Domain.csproj", "Base/src/DredgeAI.Base.Domain/"]
COPY ["Base/src/DredgeAI.Base.Application.Contracts/DredgeAI.Base.Application.Contracts.csproj", "Base/src/DredgeAI.Base.Application.Contracts/"]
COPY ["Base/src/DredgeAI.Base.Application/DredgeAI.Base.Application.csproj", "Base/src/DredgeAI.Base.Application/"]
COPY ["Base/src/DredgeAI.Base.EntityFrameworkCore/DredgeAI.Base.EntityFrameworkCore.csproj", "Base/src/DredgeAI.Base.EntityFrameworkCore/"]
COPY ["Base/src/DredgeAI.Base.HttpApi/DredgeAI.Base.HttpApi.csproj", "Base/src/DredgeAI.Base.HttpApi/"]
COPY ["Base/src/DredgeAI.Base.HttpApi.Client/DredgeAI.Base.HttpApi.Client.csproj", "Base/src/DredgeAI.Base.HttpApi.Client/"]
COPY ["Base/src/DredgeAI.Base.Host/DredgeAI.Base.Host.csproj", "Base/src/DredgeAI.Base.Host/"]

# Base服务依赖业务服务中的权限点配置信息
COPY ["Business/src/DredgeAI.Business.Domain.Shared/DredgeAI.Business.Domain.Shared.csproj", "Business/src/DredgeAI.Business.Domain.Shared/"]
COPY ["Business/src/DredgeAI.Business.Application.Contracts/DredgeAI.Business.Application.Contracts.csproj", "Business/src/DredgeAI.Business.Application.Contracts/"]
COPY ["ExternalSync/DataLink/src/DredgeAI.ExternalSync.DataLink.Application.Contracts/DredgeAI.ExternalSync.DataLink.Application.Contracts.csproj", "ExternalSync/DataLink/src/DredgeAI.ExternalSync.DataLink.Application.Contracts/"]

RUN dotnet restore "Base/src/DredgeAI.Base.Host/DredgeAI.Base.Host.csproj"

# 复制全部源码（.dockerignore 已排除 bin/obj/Logs）
COPY . .

# 编译发布
RUN dotnet publish "Base/src/DredgeAI.Base.Host/DredgeAI.Base.Host.csproj" \
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

ENTRYPOINT ["dotnet", "DredgeAI.Base.Host.dll"]
