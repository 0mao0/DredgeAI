#!/usr/bin/env pwsh
# 生成 Docker Compose 部署文件
# 用法: ./publish.ps1 [类别] [输出根目录名] [-NoDashboard]
# 类别: frontend | python | dotnet | all（默认 all）
# -NoDashboard: 发布产物不含 docker-compose-dashboard
# 默认输出目录: ./aspire-output/<类别>（仓库根目录下）

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [string]$Category = 'all',

    [Parameter(Position = 1)]
    [string]$OutputRootName = 'aspire-output',

    [switch]$NoDashboard
)

$ErrorActionPreference = 'Stop'

# === 配置 ===
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$AppHostDir = Join-Path $ScriptDir 'backend\DredgeAI.AppHost'
$OutputDir = Join-Path $ScriptDir (Join-Path $OutputRootName $Category.ToLowerInvariant())

# 避免 sandbox 无法写入 ~/.aspire 导致命令挂起
if (-not $env:ASPIRE_LOG_PATH) {
    $env:ASPIRE_LOG_PATH = Join-Path $env:TEMP 'aspire-logs'
}
New-Item -ItemType Directory -Force -Path $env:ASPIRE_LOG_PATH | Out-Null

# === 前置检查 ===
$TierMap = @{ frontend = 'frontend'; python = 'python'; dotnet = 'backend'; all = 'all' }
if (-not $TierMap.ContainsKey($Category.ToLowerInvariant())) {
    Write-Error "错误: 非法类别 '$Category'，合法值: frontend | python | dotnet | all"
    exit 1
}
$Tier = $TierMap[$Category.ToLowerInvariant()]

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error '错误: 未找到 dotnet 命令，请先安装 .NET SDK'
    exit 1
}

if (-not (Get-Command aspire -ErrorAction SilentlyContinue)) {
    Write-Error '错误: 未找到 aspire 命令，请安装 Aspire 工作负载: dotnet workload update'
    exit 1
}

if (-not (Test-Path $AppHostDir -PathType Container)) {
    Write-Error "错误: 未找到 AppHost 目录: $AppHostDir"
    exit 1
}

Set-Location $AppHostDir

Write-Host '=========================================='
Write-Host ' Aspire Publish'
Write-Host " 发布类别: $Category (tier=$Tier)"
Write-Host " Dashboard: $(if ($NoDashboard) { '不含' } else { '包含' })"
Write-Host " 项目目录: $AppHostDir"
Write-Host " 输出目录: $OutputDir"
Write-Host " 日志目录: $env:ASPIRE_LOG_PATH"
Write-Host '=========================================='

# === 清理旧输出 ===
if (Test-Path $OutputDir) {
    Write-Host "清理旧输出目录: $OutputDir"
    Remove-Item -Recurse -Force $OutputDir
}

# === 执行发布 ===
Write-Host '开始执行 aspire publish...'
$PublishArgs = @('publish', '--output-path', $OutputDir, '--', "--tier=$Tier")
if ($NoDashboard) { $PublishArgs += '--dashboard=false' }
& aspire @PublishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "aspire publish 失败，退出码: $LASTEXITCODE"
    exit $LASTEXITCODE
}

# === 结果校验 ===
$ComposeFile = Join-Path $OutputDir 'docker-compose.yaml'
$EnvFile = Join-Path $OutputDir '.env'

if (-not (Test-Path $ComposeFile)) {
    Write-Error "错误: 生成失败，未找到 $ComposeFile"
    exit 1
}

Write-Host ''
Write-Host '=========================================='
Write-Host ' 发布成功'
Write-Host '=========================================='
Write-Host " docker-compose: $ComposeFile"
Write-Host " .env 模板:      $EnvFile"
Write-Host ''
Write-Host '下一步:'
Write-Host "  1. 编辑 $EnvFile 填入实际参数值"
Write-Host "  2. docker compose -f $ComposeFile --env-file $EnvFile up -d"
Write-Host '=========================================='
