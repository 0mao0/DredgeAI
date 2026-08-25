# 构建上下文：DredgeAI/
# 构建命令：docker build -f Auth/src/DredgeAI.Auth.Host/Dockerfile DredgeAI/
# 说明：Auth 服务（OpenIddict + Identity），颁发 JWT Token，含登录页面（wwwroot）

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
COPY ["Auth/src/DredgeAI.Auth.Domain.Shared/DredgeAI.Auth.Domain.Shared.csproj", "Auth/src/DredgeAI.Auth.Domain.Shared/"]
COPY ["Auth/src/DredgeAI.Auth.Domain/DredgeAI.Auth.Domain.csproj", "Auth/src/DredgeAI.Auth.Domain/"]
COPY ["Auth/src/DredgeAI.Auth.Application.Contracts/DredgeAI.Auth.Application.Contracts.csproj", "Auth/src/DredgeAI.Auth.Application.Contracts/"]
COPY ["Auth/src/DredgeAI.Auth.Application/DredgeAI.Auth.Application.csproj", "Auth/src/DredgeAI.Auth.Application/"]
COPY ["Auth/src/DredgeAI.Auth.EntityFrameworkCore/DredgeAI.Auth.EntityFrameworkCore.csproj", "Auth/src/DredgeAI.Auth.EntityFrameworkCore/"]
COPY ["Auth/src/DredgeAI.Auth.HttpApi/DredgeAI.Auth.HttpApi.csproj", "Auth/src/DredgeAI.Auth.HttpApi/"]
COPY ["Auth/src/DredgeAI.Auth.HttpApi.Client/DredgeAI.Auth.HttpApi.Client.csproj", "Auth/src/DredgeAI.Auth.HttpApi.Client/"]
COPY ["Auth/src/DredgeAI.Auth.Host/DredgeAI.Auth.Host.csproj", "Auth/src/DredgeAI.Auth.Host/"]

COPY ["Base/src/DredgeAI.Base.Domain.Shared/DredgeAI.Base.Domain.Shared.csproj", "Base/src/DredgeAI.Base.Domain.Shared/"]
COPY ["Base/src/DredgeAI.Base.Application.Contracts/DredgeAI.Base.Application.Contracts.csproj", "Base/src/DredgeAI.Base.Application.Contracts/"]
COPY ["Iot/src/DredgeAI.Iot.Domain.Shared/DredgeAI.Iot.Domain.Shared.csproj", "Iot/src/DredgeAI.Iot.Domain.Shared/"]
COPY ["Iot/src/DredgeAI.Iot.Application.Contracts/DredgeAI.Iot.Application.Contracts.csproj", "Iot/src/DredgeAI.Iot.Application.Contracts/"]
COPY ["ExternalSync/DataLink/src/DredgeAI.ExternalSync.DataLink.Application.Contracts/DredgeAI.ExternalSync.DataLink.Application.Contracts.csproj", "ExternalSync/DataLink/src/DredgeAI.ExternalSync.DataLink.Application.Contracts/"]

COPY ["Business/src/DredgeAI.Business.Domain.Shared/DredgeAI.Business.Domain.Shared.csproj", "Business/src/DredgeAI.Business.Domain.Shared/"]
COPY ["Business/src/DredgeAI.Business.Application.Contracts/DredgeAI.Business.Application.Contracts.csproj", "Business/src/DredgeAI.Business.Application.Contracts/"]
RUN dotnet restore "Auth/src/DredgeAI.Auth.Host/DredgeAI.Auth.Host.csproj"

# 复制全部源码（.dockerignore 已排除 bin/obj/Logs）
COPY . .

# 编译发布
RUN dotnet publish "Auth/src/DredgeAI.Auth.Host/DredgeAI.Auth.Host.csproj" \
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

ENTRYPOINT ["dotnet", "DredgeAI.Auth.Host.dll"]
