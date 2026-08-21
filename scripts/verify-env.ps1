#requires -Version 5.1
<#
.SYNOPSIS
  生产/共享环境 .env 严格检查（上线前阻断脚本）。

.DESCRIPTION
  运行时敏感值只允许来自部署 .env（或等价密钥注入），不允许出现在
  appsettings*.json / 代码默认值 / CI 日志 / git 历史中。
  任一检查失败即退出码 1 并输出 FAIL 原因。

.EXAMPLE
  .\scripts\verify-env.ps1                      # 检查当前目录 .env
  .\scripts\verify-env.ps1 -EnvPath C:\deploy\.env
#>
param(
    [string]$EnvPath = ".env"
)

$ErrorActionPreference = "Stop"
$failures = 0

function Assert-Value([string]$name) {
    if (-not $envMap.ContainsKey($name) -or [string]::IsNullOrWhiteSpace($envMap[$name])) {
        Write-Host "FAIL: $name 为空" -ForegroundColor Red
        $script:failures++
        return $false
    }
    return $true
}

function Assert-Forbidden([string]$value, [string]$label, [string]$needle) {
    if ($value -like "*$needle*") {
        Write-Host "FAIL: $label 包含禁止的弱值/默认值: $needle" -ForegroundColor Red
        $script:failures++
    }
}

# 读取 .env（UTF8 兼容 BOM）
if (-not (Test-Path -LiteralPath $EnvPath)) {
    Write-Host "FAIL: 找不到 $EnvPath" -ForegroundColor Red
    exit 1
}
$lines = Get-Content -LiteralPath $EnvPath -Encoding UTF8 | Where-Object { $_ -match '^\s*[A-Za-z_][A-Za-z0-9_]*=' }
$envMap = @{}
foreach ($line in $lines) {
    $k, $v = $line -split '=', 2
    $envMap[$k.Trim()] = $v.Trim()
}

Write-Host "== 必填变量 =="
foreach ($v in @(
    'BIDCOMPARE_DB_CONNECTION',
    'STRING_ENCRYPTION_PASSPHRASE',
    'ANGINEER_API_KEY',
    'LLM_CONFIGS',
    'AI_GATEWAY_BASE_URL',
    'AI_GATEWAY_API_TOKEN',
    'AI_GATEWAY_INGEST_TOKEN')) {
    Assert-Value $v | Out-Null
}

Write-Host "== 禁止的弱值/默认值 =="
# 历史已泄露值（含前缀匹配）：jyl2qty 为旧 ABP passphrase 前缀，minioadmin 为 MinIO 出厂默认
$forbiddenNeedles = @('jyl2qty', 'minioadmin', 'CHANGE_ME', 'Password=postgres', 'password=postgres', 'example')
$joined = $envMap.Values -join ' '
foreach ($f in $forbiddenNeedles) {
    Assert-Forbidden $joined "env 整体" $f
}

Write-Host "== 令牌约束 =="
if ($envMap['AI_GATEWAY_API_TOKEN'] -eq $envMap['AI_GATEWAY_INGEST_TOKEN']) {
    Write-Host "FAIL: 出站与入站网关令牌不得相同" -ForegroundColor Red
    $failures++
}
if ($envMap['AI_GATEWAY_API_TOKEN'].Length -lt 32 -or $envMap['AI_GATEWAY_INGEST_TOKEN'].Length -lt 32) {
    Write-Host "FAIL: 网关令牌长度不足 32" -ForegroundColor Red
    $failures++
}
if ($envMap['STRING_ENCRYPTION_PASSPHRASE'].Length -lt 16) {
    Write-Host "FAIL: STRING_ENCRYPTION_PASSPHRASE 过短（建议 openssl rand -hex 32 生成）" -ForegroundColor Red
    $failures++
}

Write-Host "== LLM_CONFIGS 结构 =="
try {
    $llm = $envMap['LLM_CONFIGS'] | ConvertFrom-Json
    $active = @($llm | Where-Object { $_.enabled -ne $false -and -not [string]::IsNullOrWhiteSpace($_.api_key) })
    if ($active.Count -eq 0) {
        Write-Host "FAIL: LLM_CONFIGS 没有启用的配置项（enabled 且 api_key 非空）" -ForegroundColor Red
        $failures++
    }
} catch {
    Write-Host "FAIL: LLM_CONFIGS 不是合法 JSON: $($_.Exception.Message)" -ForegroundColor Red
    $failures++
}

Write-Host "== 生产开关 =="
if ($envMap.ContainsKey('AUTH_REQUIRE_HTTPS_METADATA') -and $envMap['AUTH_REQUIRE_HTTPS_METADATA'] -ne 'true') {
    Write-Host "FAIL: AUTH_REQUIRE_HTTPS_METADATA 必须为 true" -ForegroundColor Red
    $failures++
}
if ($envMap.ContainsKey('SWAGGER_ENABLED') -and $envMap['SWAGGER_ENABLED'] -eq 'true') {
    Write-Host "FAIL: SWAGGER_ENABLED 生产必须为 false" -ForegroundColor Red
    $failures++
}

Write-Host ""
if ($failures -eq 0) {
    Write-Host "PASS: 全部检查通过，可上线" -ForegroundColor Green
    exit 0
} else {
    Write-Host "FAIL: 共 $failures 项未通过，禁止上线" -ForegroundColor Red
    exit 1
}
