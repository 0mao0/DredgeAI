# 生产环境 .env 严格检查清单

> 适用：任何共享/生产/演示环境上线前。运行时敏感值一律只允许来自部署环境的 `.env`（或等价密钥注入机制），
> **不允许**出现在 appsettings*.json、代码默认值、CI 日志、前端构建产物或 git 历史中。

## 0. 验收红线（违反任一条即禁止上线）

- 存在任一密钥为空、为模板默认值（`minioadmin` / 旧 ABP passphrase（前缀 `jyl2qty`，完整值见内部记录）/ `CHANGE_ME` / `example`）或为「本地开发值」（`postgres/postgres`）。
- `LLM_CONFIGS` 缺失、JSON 非法、含空 `api_key` 的启用项、或所有项 `enabled` 均为 false。
- `AI_GATEWAY_API_TOKEN` / `AI_GATEWAY_INGEST_TOKEN` 为空（生产禁止关闭网关令牌校验）。
- `.env` 权限过宽（应仅部署用户可读）、被提交进仓库、或出现在备份/日志/截图里。
- 后端日志中出现了密钥明文（代码约定只打印「已配置」，若出现即视为事故）。

## 1. 必填变量清单

| 变量 | 用途 | 生产要求 | 生成方式 |
|---|---|---|---|
| `BIDCOMPARE_DB_CONNECTION` | 主库连接串 | 必填；禁止 `postgres/postgres`；建议专用账号 + 强密码 | `openssl rand -base64 24` |
| `STRING_ENCRYPTION_PASSPHRASE` | ABP 设置项加密 | 必填；禁止模板默认值 | `openssl rand -hex 32` |
| `ANGINEER_API_KEY` | AnGIneer 文档解析 | 必填；非空 | AnGIneer 侧签发 |
| `LLM_CONFIGS` | 模型配置（JSON 数组） | 必填；JSON 合法；每项 `api_key` 非空；至少 1 项启用 | 供应商控制台签发 |
| `AI_GATEWAY_BASE_URL` | ABP → ai-gateway 地址 | 必填；内网地址，禁止暴露公网 | 部署拓扑决定 |
| `AI_GATEWAY_API_TOKEN` | ABP → 网关出站令牌（`X-API-Key`） | 必填；与 `AI_GATEWAY_INGEST_TOKEN` 必须不同 | `openssl rand -hex 32` |
| `AI_GATEWAY_INGEST_TOKEN` | 网关 → ABP 用量上报（`X-Gateway-Token`） | 必填；与出站令牌不同 | `openssl rand -hex 32` |
| `AUTH_REQUIRE_HTTPS_METADATA` | 认证元数据 HTTPS 强制 | 生产必须 `true` | — |
| `SWAGGER_ENABLED` | Swagger 开关 | 生产必须 `false` | — |

## 2. 条件必填变量

| 变量 | 触发条件 | 生产要求 |
|---|---|---|
| `STORAGE_S3_ACCESSKEY` / `STORAGE_S3_SECRETKEY` | `Storage:Provider=S3` | 必填；禁止 `minioadmin`；建议为 MinIO 最小权限专用账号 |
| `STORAGE_LOCAL_SIGNING_SECRET` | `Storage:Provider=Local` | 必填高强度随机值；生产不建议 Local |

## 3. 可选/按需

- `ANGINEER_*`（超时/重试/熔断参数，见 angineer-ai-inference 文档）
- `OpenIddict__Applications__*`（ClientId/RootUrl，须与部署域名一致）

## 4. 自动化检查

在部署机（或 CI 的密钥检查阶段）运行以下 PowerShell 脚本，任一断言失败即阻断发布：

```powershell
# 在仓库根或部署 .env 所在目录运行；$envPath 指向要检查的 .env
param([string]$envPath = ".env")

$ErrorActionPreference = "Stop"
$lines = Get-Content $envPath -Encoding UTF8 | Where-Object { $_ -match '^\s*[A-Za-z_][A-Za-z0-9_]*=' }
$envMap = @{}
foreach ($line in $lines) {
    $k, $v = $line -split '=', 2
    $envMap[$k.Trim()] = $v.Trim()
}

function Assert-Value($name) {
    if (-not $envMap.ContainsKey($name) -or [string]::IsNullOrWhiteSpace($envMap[$name])) {
        throw "FAIL: $name 为空"
    }
}

# 必填
foreach ($v in @('BIDCOMPARE_DB_CONNECTION','STRING_ENCRYPTION_PASSPHRASE','ANGINEER_API_KEY',
                 'LLM_CONFIGS','AI_GATEWAY_BASE_URL','AI_GATEWAY_API_TOKEN','AI_GATEWAY_INGEST_TOKEN')) {
    Assert-Value $v
}

# 禁止的弱值/默认值
$forbidden = @('minioadmin','jyl2qty','CHANGE_ME','Password=postgres','password=postgres','example')
$joined = $envMap.Values -join ' '
foreach ($f in $forbidden) {
    if ($joined -like "*$f*") { throw "FAIL: 检测到禁止的弱值/默认值: $f" }
}

# 令牌与弱口令约束
if ($envMap['AI_GATEWAY_API_TOKEN'] -eq $envMap['AI_GATEWAY_INGEST_TOKEN']) {
    throw "FAIL: 出站与入站网关令牌不得相同"
}
if ($envMap['AI_GATEWAY_API_TOKEN'].Length -lt 32 -or $envMap['AI_GATEWAY_INGEST_TOKEN'].Length -lt 32) {
    throw "FAIL: 网关令牌长度不足 32"
}

# LLM_CONFIGS JSON 校验
$configs = $envMap['LLM_CONFIGS'] | ConvertFrom-Json
if ($configs.Count -lt 1) { throw "FAIL: LLM_CONFIGS 为空" }
$enabled = @($configs | Where-Object { $_.enabled -ne $false })
if ($enabled.Count -lt 1) { throw "FAIL: LLM_CONFIGS 无启用项" }
foreach ($c in $enabled) {
    if ([string]::IsNullOrWhiteSpace($c.api_key)) { throw "FAIL: 模型 $($c.name) 缺少 api_key" }
}

# 安全开关
if ($envMap['AUTH_REQUIRE_HTTPS_METADATA'] -ne 'true') { throw "FAIL: AUTH_REQUIRE_HTTPS_METADATA 必须为 true" }
if ($envMap['SWAGGER_ENABLED'] -eq 'true') { throw "FAIL: SWAGGER_ENABLED 生产必须为 false" }

Write-Host "PASS: 生产 .env 检查通过" -ForegroundColor Green
```

## 5. 常规纪律

- 文件权限：仅部署账号可读写（Linux `chmod 600` / Windows ACL），禁止进入版本库（`git check-ignore .env` 必须命中）。
- 双文件同步：`LLM_CONFIGS` 的 api_key 若同时存在于 AnGIneer 与 DredgeAI 两个 `.env`，轮换时必须同步修改两处。
- 日志纪律：任何日志/告警/错误上报不得打印令牌明文（后端已按「已配置」方式打印）。
- 轮换流程：见 [key-rotation.md](./key-rotation.md)；网关令牌轮换后需同时重启 ai-gateway 与后端。
- 上线前演练：先运行上面脚本，再执行 `.\start.ps1 -NoBrowser` 全量健康检查，并触发一次真实 LLM 调用确认链路。
