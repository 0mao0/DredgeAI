# DredgeAI 后端数据库手动执行清单

> 本计划**不自动执行**任何建库/建 schema/迁移脚本。以下命令需手动执行。
> 前置：PostgreSQL 客户端 `psql`；`dotnet` 指 `C:\Program Files\dotnet\dotnet.exe`（SDK 10.0.303）。
> 目标库：`dredge_ai`（单库），Auth 与 Base 共用 schema `dredge_base`。

## 1. 建库与建 schema（幂等）

```bash
# 1.1 建库（已存在则忽略报错）
psql "Host=test.bim-ace.com;Port=10111;Username=postgres;Password=Shiw@123" -c "CREATE DATABASE dredge_ai"

# 1.2 建 schema（幂等）
psql "Host=test.bim-ace.com;Port=10111;Username=postgres;Password=Shiw@123;Database=dredge_ai" -c "CREATE SCHEMA IF NOT EXISTS dredge_base"
```

> 若 `test.bim-ace.com:10111` 不可达，将上述连接串替换为本机 PostgreSQL：
> `Host=localhost;Port=5432;Username=postgres;Password=<本机密码>`，数据库仍为 `dredge_ai`、schema 仍为 `dredge_base`。

## 2. 应用迁移（按顺序）

```bash
# 2.1 Auth：OpenIddict + Identity + 各管理模块表（schema dredge_base）
"C:\Program Files\dotnet\dotnet.exe" ef database update --project backend\Auth\src\DredgeAI.Auth.Host --context AuthServerDbContext

# 2.2 Base：字典/菜单/组织/用户扩展/文件管理等表（schema dredge_base）
"C:\Program Files\dotnet\dotnet.exe" ef database update --project backend\Base\src\DredgeAI.Base.Host --context BaseServerDbContext
```

> 命令在仓库根目录（`D:\codes\dredge-ai\`）执行；两个 Host 均带 `IDesignTimeDbContextFactory`，无需指定 startup project。
> 连接串来自各 Host 的 `appsettings.json`：`Server=test.bim-ace.com;Port=10111;Database=dredge_ai;SearchPath=dredge_base;Uid=postgres;Pwd=Shiw@123;`。

## 3. 执行后检查清单（B 段验证）

1. **Auth（https 7233）**：启动 `DredgeAI.Auth.Host` → `GET /health` 返回 Healthy；`GET /.well-known/openid-configuration` 返回 JSON 且 `issuer` = `https://localhost:7233`；首启日志出现 `DredgeAI_Web` / `DredgeAI_App` / `DredgeAI_Swagger` 客户端种子写入。
2. **Base（https 7234）**：启动 `DredgeAI.Base.Host` → `GET /health` 返回 Healthy；`/swagger/v1/swagger.json` 200 且含 `/api/base/` 路径；受保护接口无 token 返回 401（JWT 验证指向 7233 生效）。
3. **Gateway（https 7237）**：启动 `DredgeAI.Gateway.Host` → `GET https://localhost:7237/.well-known/openid-configuration` 经 YARP 转发返回与第 1 步相同 JSON；连发超过 100 次/10s 触发 429。
4. **Aspire**：`dotnet run --project backend\DredgeAI.AppHost` → dashboard 中 `auth-service` / `base-service` / `gateway-service` 全部 Running 且健康检查通过（base 等 auth、gateway 等两者）。
