param(
    [switch]$TailLogs,
    [switch]$NoBrowser
)

# DredgeAI Startup Script（比标后端 + 算法服务 + 前端；AnGIneer 仅检测）
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$rootDir = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$logsDir = Join-Path $rootDir "logs"
$compareAlgoLogPath = Join-Path $logsDir "compare-algo.log"
$backendLogPath = Join-Path $logsDir "backend.log"
$frontendLogPath = Join-Path $logsDir "frontend.log"
$compareAlgoPidPath = Join-Path $logsDir "compare-algo.pid"
$backendPidPath = Join-Path $logsDir "backend.pid"
$frontendPidPath = Join-Path $logsDir "frontend.pid"

# 端口约定
$postgresPort = 5432
$compareAlgoPort = 8100
$backendPort = 44361
$frontendPort = 5373
$angineerPort = 8789

$backendUrl = "https://localhost:$backendPort"
$compareAlgoUrl = "http://localhost:$compareAlgoPort"
$frontendUrl = "http://localhost:$frontendPort"
$angineerUrl = "http://localhost:$angineerPort"

# 关键路径
$compareAlgoDir = Join-Path $rootDir "services\compare-algo"
$compareAlgoPython = Join-Path $compareAlgoDir ".venv\Scripts\python.exe"
$backendProject = Join-Path $rootDir "backend\DredgeAI.BidCompare\src\DredgeAI.BidCompare.HttpApi.Host"
$frontendDir = Join-Path $rootDir "user-web"
$postgresContainer = "bidcompare-postgres"

# 递归停止进程树（含子进程）
function Stop-ProcessTree {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ProcessId
    )

    $process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
    if (-not $process) { return }

    $children = Get-CimInstance Win32_Process | Where-Object {
        $_.ParentProcessId -eq $ProcessId
    }
    foreach ($child in $children) {
        Stop-ProcessTree -ProcessId $child.ProcessId
    }

    Stop-Process -Id $ProcessId -Force -ErrorAction SilentlyContinue
}

# 按 PID 文件停止服务进程
function Stop-ServiceProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServiceName,
        [Parameter(Mandatory = $true)]
        [string]$PidPath
    )

    if (-not (Test-Path $PidPath)) { return }

    $pidText = (Get-Content $PidPath -Raw -ErrorAction SilentlyContinue).Trim()
    if ($pidText -match '^\d+$') {
        $existingProcess = Get-Process -Id ([int]$pidText) -ErrorAction SilentlyContinue
        if ($existingProcess) {
            Write-Host "Stopping stale $ServiceName process tree: PID $pidText" -ForegroundColor DarkYellow
            Stop-ProcessTree -ProcessId $existingProcess.Id
        }
    }

    Remove-Item $PidPath -Force -ErrorAction SilentlyContinue
}

# 停止占用指定端口的进程（重启前清理，保证幂等）
function Stop-PortProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [int]$Port
    )

    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if (-not $connections) { return }

    $killedPids = @{}
    foreach ($conn in $connections) {
        $connPid = $conn.OwningProcess
        if ($killedPids.ContainsKey($connPid)) { continue }
        $killedPids[$connPid] = $true
        $proc = Get-Process -Id $connPid -ErrorAction SilentlyContinue
        if ($proc) {
            Stop-ProcessTree -ProcessId $connPid
            Write-Host "Stopped stale process on port ${Port} (${Label}): PID $connPid" -ForegroundColor DarkYellow
        }
    }
}

# 启动隐藏后台服务进程，日志 + PID 文件
function Start-ServiceProcess {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServiceName,
        [Parameter(Mandatory = $true)]
        [string]$ServiceCommand,
        [Parameter(Mandatory = $true)]
        [string]$LogPath,
        [Parameter(Mandatory = $true)]
        [string]$PidPath
    )

    Stop-ServiceProcess -ServiceName $ServiceName -PidPath $PidPath

    $escapedRootDir = $rootDir.Replace("'", "''")
    $escapedLogPath = $LogPath.Replace("'", "''")
    $escapedCommand = $ServiceCommand.Replace("'", "''")
    $startupBanner = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] starting: $ServiceCommand"
    $startupScript = @"
Set-Location '$escapedRootDir'
'$startupBanner' | Out-File -FilePath '$escapedLogPath' -Encoding utf8 -Append
Invoke-Expression '$escapedCommand' *>> '$escapedLogPath'
"@

    $process = Start-Process `
        -FilePath "powershell.exe" `
        -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $startupScript) `
        -WindowStyle Hidden `
        -PassThru

    Set-Content -Path $PidPath -Value $process.Id -Encoding ascii
    return $process
}

# 兼容 PS5.1/7 的 HTTPS 请求（自签名证书跳过校验）
function Invoke-WebRequestSafe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        [int]$TimeoutSec = 3
    )

    if ($PSVersionTable.PSVersion.Major -ge 7) {
        return Invoke-WebRequest -Uri $Uri -TimeoutSec $TimeoutSec -SkipCertificateCheck -UseBasicParsing -ErrorAction Stop
    }
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
    return Invoke-WebRequest -Uri $Uri -TimeoutSec $TimeoutSec -UseBasicParsing -ErrorAction Stop
}

# 等待 HTTP 服务就绪
function Test-HttpHealth {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [string]$Url,
        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $attempts = 0

    while ((Get-Date) -lt $deadline) {
        $attempts++
        try {
            $response = Invoke-WebRequestSafe -Uri $Url -TimeoutSec 3
            if ($response.StatusCode -eq 200) {
                Write-Host "  $Label health check passed (attempt $attempts)" -ForegroundColor Green
                return $true
            }
        } catch {
            # 启动中，继续等待
        }
        Write-Host "  Waiting for $Label... (attempt $attempts)" -ForegroundColor DarkGray
        Start-Sleep -Seconds 2
    }

    return $false
}

# 等待端口监听
function Test-PortListening {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Label,
        [Parameter(Mandatory = $true)]
        [int]$Port,
        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $attempts = 0

    while ((Get-Date) -lt $deadline) {
        $attempts++
        if (Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue) {
            Write-Host "  $Label port $Port is listening (attempt $attempts)" -ForegroundColor Green
            return $true
        }
        Start-Sleep -Seconds 2
    }

    return $false
}

# 跟随日志（.\\start.ps1 -TailLogs）
function Watch-ServiceLogs {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$LogPaths
    )

    $existingLogs = @($LogPaths | Where-Object { Test-Path $_ })
    if (-not $existingLogs.Count) {
        Write-Warning "No log files found. Run .\start.ps1 first."
        return
    }

    Write-Host "Following logs..." -ForegroundColor Cyan
    $existingLogs | ForEach-Object { Write-Host "  $_" -ForegroundColor DarkGray }
    Get-Content -Path $existingLogs -Tail 30 -Wait
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   DredgeAI Startup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. 前置检查
Write-Host "[1/5] Checking prerequisites..." -ForegroundColor Yellow
$nodeVer = node --version 2>$null
if (-not $nodeVer) { Write-Error "Node.js not found!"; exit 1 }
Write-Host "  Node.js $nodeVer" -ForegroundColor DarkGray

$pnpmVer = pnpm --version 2>$null
if (-not $pnpmVer) { Write-Error "pnpm not found!"; exit 1 }
Write-Host "  pnpm $pnpmVer" -ForegroundColor DarkGray

$dotnetCmd = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnetCmd) {
    $dotnetExe = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet\dotnet.exe"
    if (Test-Path $dotnetExe) {
        $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
        Write-Host "  dotnet added from LOCALAPPDATA" -ForegroundColor DarkGray
    } else {
        Write-Error "dotnet not found!"; exit 1
    }
}
Write-Host "  dotnet $((dotnet --version))" -ForegroundColor DarkGray

if (-not (Test-Path $compareAlgoPython)) {
    Write-Error "compare-algo venv not found: $compareAlgoPython"; exit 1
}
Write-Host "  compare-algo venv OK" -ForegroundColor DarkGray

if (-not (Test-Path (Join-Path $frontendDir "package.json"))) {
    Write-Error "user-web not found: $frontendDir"; exit 1
}
Write-Host "  user-web OK" -ForegroundColor DarkGray

if (-not (Test-Path $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir | Out-Null
}

if ($TailLogs) {
    Watch-ServiceLogs -LogPaths @($backendLogPath, $compareAlgoLogPath, $frontendLogPath)
    exit 0
}

# 2. 清理旧进程（幂等重启）
Write-Host "[2/5] Cleaning up stale processes..." -ForegroundColor Yellow
Stop-PortProcess -Label "compare-algo" -Port $compareAlgoPort
Stop-PortProcess -Label "Backend" -Port $backendPort
Stop-PortProcess -Label "Frontend" -Port $frontendPort

# 3. PostgreSQL（Docker）
Write-Host "[3/5] Ensuring PostgreSQL (Docker)..." -ForegroundColor Yellow
$dockerCmd = Get-Command docker -ErrorAction SilentlyContinue
if (-not $dockerCmd) {
    Write-Warning "Docker not found; PostgreSQL must be started manually (port $postgresPort)."
} else {
    try {
        $existing = docker ps -a --filter "name=^/$postgresContainer$" --format "{{.Names}}" 2>$null
        if ($existing) {
            docker start $postgresContainer | Out-Null
            Write-Host "  Started container $postgresContainer" -ForegroundColor Green
        } else {
            docker run -d --name $postgresContainer `
                -e POSTGRES_USER=postgres `
                -e POSTGRES_PASSWORD=postgres `
                -e POSTGRES_DB=BidCompare `
                -p "${postgresPort}:5432" `
                postgres:16 | Out-Null
            Write-Host "  Created container $postgresContainer" -ForegroundColor Green
        }
    } catch {
        Write-Warning "PostgreSQL start failed: $($_.Exception.Message)"
    }
}
$postgresReady = Test-PortListening -Label "PostgreSQL" -Port $postgresPort -TimeoutSeconds 30
if (-not $postgresReady) {
    Write-Host "  WARNING: PostgreSQL not ready; backend may fail to start." -ForegroundColor Red
}

# 4. 启动服务
Write-Host "[4/5] Starting services..." -ForegroundColor Yellow
Write-Host "      compare-algo: $compareAlgoUrl" -ForegroundColor Green
Write-Host "      Backend:      $backendUrl" -ForegroundColor Green
Write-Host "      Frontend:     $frontendUrl" -ForegroundColor Green

$escapedAlgoDir = $compareAlgoDir.Replace("'", "''")
$escapedPython = $compareAlgoPython.Replace("'", "''")
$compareAlgoCommand = "Set-Location '$escapedAlgoDir'; & '$escapedPython' -m uvicorn app.main:app --host 127.0.0.1 --port $compareAlgoPort"
$compareAlgoProcess = Start-ServiceProcess -ServiceName "compare-algo" -ServiceCommand $compareAlgoCommand -LogPath $compareAlgoLogPath -PidPath $compareAlgoPidPath

$escapedProject = $backendProject.Replace("'", "''")
$backendCommand = "`$env:PATH=`"`$env:LOCALAPPDATA\Microsoft\dotnet;`$env:PATH`"; dotnet run --project '$escapedProject' --launch-profile 'DredgeAI.BidCompare.HttpApi.Host'"
$backendProcess = Start-ServiceProcess -ServiceName "Backend" -ServiceCommand $backendCommand -LogPath $backendLogPath -PidPath $backendPidPath

$escapedFrontendDir = $frontendDir.Replace("'", "''")
$frontendCommand = "Set-Location '$escapedFrontendDir'; pnpm dev"
$frontendProcess = Start-ServiceProcess -ServiceName "Frontend" -ServiceCommand $frontendCommand -LogPath $frontendLogPath -PidPath $frontendPidPath

Write-Host "      Logs: $logsDir" -ForegroundColor DarkGray
Write-Host "      Backend PID: $($backendProcess.Id), compare-algo PID: $($compareAlgoProcess.Id), frontend PID: $($frontendProcess.Id)" -ForegroundColor DarkGray

# 5. 健康检查
Write-Host "[5/5] Waiting for services..." -ForegroundColor Yellow
$compareAlgoHealthy = Test-HttpHealth -Label "compare-algo" -Url "$compareAlgoUrl/healthz" -TimeoutSeconds 60
$backendHealthy = Test-HttpHealth -Label "Backend" -Url "$backendUrl/swagger/v1/swagger.json" -TimeoutSeconds 180
$frontendHealthy = Test-HttpHealth -Label "Frontend" -Url $frontendUrl -TimeoutSeconds 60

# AnGIneer 检测（不属于 DredgeAI 仓库，仅提示）
$angineerReady = Test-HttpHealth -Label "AnGIneer" -Url "$angineerUrl/docs" -TimeoutSeconds 5
if (-not $angineerReady) {
    Write-Host ""
    Write-Host "WARNING: AnGIneer ($angineerUrl) is not responding." -ForegroundColor Red
    Write-Host "  Start it separately from D:\AI\AnGIneer (e.g. .\start.ps1), then re-run this script." -ForegroundColor Red
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "   Startup Summary" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ("  compare-algo {0}" -f $(if ($compareAlgoHealthy) { "OK" } else { "FAILED" })) -ForegroundColor $(if ($compareAlgoHealthy) { "Green" } else { "Red" })
Write-Host ("  Backend      {0}" -f $(if ($backendHealthy) { "OK" } else { "FAILED" })) -ForegroundColor $(if ($backendHealthy) { "Green" } else { "Red" })
Write-Host ("  Frontend     {0}" -f $(if ($frontendHealthy) { "OK" } else { "FAILED" })) -ForegroundColor $(if ($frontendHealthy) { "Green" } else { "Red" })
Write-Host ("  AnGIneer     {0}" -f $(if ($angineerReady) { "OK" } else { "not running" })) -ForegroundColor $(if ($angineerReady) { "Green" } else { "DarkYellow" })
Write-Host ""
Write-Host "  Frontend: $frontendUrl" -ForegroundColor Cyan
Write-Host "  Backend Swagger: $backendUrl/swagger" -ForegroundColor Cyan
Write-Host "  Logs: $logsDir" -ForegroundColor DarkGray
Write-Host "  Tail logs with: .\start.ps1 -TailLogs" -ForegroundColor DarkGray

if ($backendHealthy -and -not $NoBrowser) {
    Start-Process $frontendUrl
}
