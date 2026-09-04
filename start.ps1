param(
    [switch]$TailLogs,
    [switch]$NoBrowser,
    [switch]$OpenBrowser
)

# DredgeAI Startup Script（Auth + 比标后端 + 算法服务 + 用户端/管理端前端；AnGIneer 仅检测）
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$rootDir = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$dataDir = Join-Path $rootDir "data"
$logsDir = Join-Path $dataDir "logs"
$postgresDataDir = Join-Path $dataDir "postgres"
$storageDir = Join-Path $dataDir "storage"
$backupDir = Join-Path $dataDir "backup"
$compareAlgoLogPath = Join-Path $logsDir "compare-algo.log"
$aiGatewayLogPath = Join-Path $logsDir "ai-gateway.log"
$backendLogPath = Join-Path $logsDir "backend.log"
$authLogPath = Join-Path $logsDir "auth.log"
$frontendLogPath = Join-Path $logsDir "frontend.log"
$adminLogPath = Join-Path $logsDir "admin-web.log"
$compareAlgoPidPath = Join-Path $logsDir "compare-algo.pid"
$aiGatewayPidPath = Join-Path $logsDir "ai-gateway.pid"
$backendPidPath = Join-Path $logsDir "backend.pid"
$authPidPath = Join-Path $logsDir "auth.pid"
$frontendPidPath = Join-Path $logsDir "frontend.pid"
$adminPidPath = Join-Path $logsDir "admin-web.pid"

# 优先使用用户目录下的真实 dotnet SDK（部分机器 C:\Program Files\dotnet 只是无 SDK 的空壳）
$localDotnetDir = Join-Path $env:LOCALAPPDATA "Microsoft\dotnet"
if (Test-Path (Join-Path $localDotnetDir "dotnet.exe")) {
    $env:PATH = "$localDotnetDir;$env:PATH"
}

# 端口约定
$postgresPort = 5432
$compareAlgoPort = 8100
$aiGatewayPort = 8200
$backendPort = 44361
$authPort = 7233
$frontendPort = 5373
$adminPort = 5374
$angineerPort = 8790

$backendUrl = "https://localhost:$backendPort"
$authUrl = "https://localhost:$authPort"
$compareAlgoUrl = "http://localhost:$compareAlgoPort"
$aiGatewayUrl = "http://localhost:$aiGatewayPort"
$frontendUrl = "http://localhost:$frontendPort"
$adminUrl = "http://localhost:$adminPort"
$angineerUrl = "http://localhost:$angineerPort"

# 关键路径
$compareAlgoDir = Join-Path $rootDir "services\compare-algo"
$compareAlgoPython = Join-Path $compareAlgoDir ".venv\Scripts\python.exe"
$aiGatewayDir = Join-Path $rootDir "services\ai-gateway"
$aiGatewayPython = Join-Path $aiGatewayDir ".venv\Scripts\python.exe"
$backendProject = Join-Path $rootDir "backend\BidCompare\src\DredgeAI.BidCompare.Host"
$authProject = Join-Path $rootDir "backend\Auth\src\DredgeAI.Auth.Host"
$frontendDir = Join-Path $rootDir "user-web"
$adminDir = Join-Path $rootDir "admin-web"
$postgresContainer = "bidcompare-postgres"

# 运行时数据目录（monorepo 约定：仓库根 data/，统一不入库）
foreach ($dir in @($dataDir, $logsDir, $postgresDataDir, $storageDir, $backupDir, (Join-Path $dataDir "base"))) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

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

    # 轮转旧日志：历史文件可能是 UTF-8 头 + UTF-16 正文混写，统一改为纯 UTF-8（无 BOM）后从干净文件开始
    if (Test-Path -LiteralPath $LogPath) {
        $rotatedLogPath = "$LogPath.1"
        if (Test-Path -LiteralPath $rotatedLogPath) {
            Remove-Item -LiteralPath $rotatedLogPath -Force -ErrorAction SilentlyContinue
        }
        Move-Item -LiteralPath $LogPath -Destination $rotatedLogPath -Force
    }

    $escapedRootDir = $rootDir.Replace("'", "''")
    $escapedLogPath = $LogPath.Replace("'", "''")
    $escapedCommand = $ServiceCommand.Replace("'", "''")
    $startupBanner = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] starting: $ServiceName"
    $startupScript = @"
Set-Location '$escapedRootDir'
`$utf8 = New-Object System.Text.UTF8Encoding(`$false)
`$writer = New-Object System.IO.StreamWriter('$escapedLogPath', `$true, `$utf8)
`$writer.AutoFlush = `$true
`$writer.WriteLine('$startupBanner')
& { Invoke-Expression '$escapedCommand' } 2>&1 | ForEach-Object { `$writer.WriteLine("`$_") }
`$writer.Close()
"@

    $encodedCommand = [Convert]::ToBase64String([System.Text.Encoding]::Unicode.GetBytes($startupScript))

    $process = Start-Process `
        -FilePath "powershell.exe" `
        -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-EncodedCommand", $encodedCommand) `
        -WindowStyle Hidden `
        -PassThru

    Set-Content -Path $PidPath -Value $process.Id -Encoding ascii
    return $process
}

# 兼容 PS5.1/7 的 HTTP 健康检查（PS5.1 的 HttpWebRequest 无法与 Kestrel 完成 ALPN/HTTP2 握手，改用 Node fetch）
function Invoke-WebRequestSafe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Uri,
        [int]$TimeoutSec = 3
    )

    $env:NODE_TLS_REJECT_UNAUTHORIZED = '0'
    $nodeScript = 'const u=process.argv[1],ms=parseInt(process.argv[2],10)*1000,t=setTimeout(()=>process.exit(2),ms);fetch(u).then(r=>{clearTimeout(t);console.log(r.status);process.exit(r.status===200?0:1)}).catch(()=>process.exit(2));'
    $output = & node -e $nodeScript $Uri $TimeoutSec 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $output) {
        throw "HTTP health check failed for $Uri"
    }
    return [pscustomobject]@{ StatusCode = [int]($output.Trim()) }
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
    Get-Content -Path $existingLogs -Tail 30 -Wait -Encoding UTF8
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

$dotnetVer = dotnet --version 2>$null
if (-not $dotnetVer -and (Test-Path (Join-Path $localDotnetDir "dotnet.exe"))) {
    $dotnetVer = & (Join-Path $localDotnetDir "dotnet.exe") --version 2>$null
}
if (-not $dotnetVer) {
    Write-Error "dotnet not found! Install .NET 8 SDK or set a working dotnet on PATH."
    exit 1
}
Write-Host "  dotnet $dotnetVer" -ForegroundColor DarkGray

if (-not (Test-Path $compareAlgoPython)) {
    Write-Error "compare-algo venv not found: $compareAlgoPython"; exit 1
}
Write-Host "  compare-algo venv OK" -ForegroundColor DarkGray

if (-not (Test-Path $aiGatewayPython)) {
    Write-Error "ai-gateway venv not found: $aiGatewayPython"; exit 1
}
Write-Host "  ai-gateway venv OK" -ForegroundColor DarkGray

if (-not (Test-Path (Join-Path $frontendDir "package.json"))) {
    Write-Error "user-web not found: $frontendDir"; exit 1
}
Write-Host "  user-web OK" -ForegroundColor DarkGray

if (-not (Test-Path (Join-Path $adminDir "package.json"))) {
    Write-Error "admin-web not found: $adminDir"; exit 1
}
Write-Host "  admin-web OK" -ForegroundColor DarkGray

if (-not (Test-Path $logsDir)) {
    New-Item -ItemType Directory -Path $logsDir | Out-Null
}

if ($TailLogs) {
    Watch-ServiceLogs -LogPaths @($backendLogPath, $authLogPath, $compareAlgoLogPath, $aiGatewayLogPath, $frontendLogPath, $adminLogPath)
    exit 0
}

# 2. 清理旧进程（幂等重启）
Write-Host "[2/5] Cleaning up stale processes..." -ForegroundColor Yellow
Stop-PortProcess -Label "compare-algo" -Port $compareAlgoPort
Stop-PortProcess -Label "ai-gateway" -Port $aiGatewayPort
Stop-PortProcess -Label "Auth" -Port $authPort
Stop-PortProcess -Label "Backend" -Port $backendPort
Stop-PortProcess -Label "Frontend" -Port $frontendPort
Stop-PortProcess -Label "Admin Web" -Port $adminPort

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
                -v "${postgresDataDir}:/var/lib/postgresql/data" `
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
Write-Host "      ai-gateway:  $aiGatewayUrl" -ForegroundColor Green
Write-Host "      Auth:        $authUrl" -ForegroundColor Green
Write-Host "      Backend:      $backendUrl" -ForegroundColor Green
Write-Host "      Frontend:     $frontendUrl" -ForegroundColor Green
Write-Host "      Admin Web:    $adminUrl" -ForegroundColor Green

$escapedAlgoDir = $compareAlgoDir.Replace("'", "''")
$escapedPython = $compareAlgoPython.Replace("'", "''")
$compareAlgoCommand = "Set-Location '$escapedAlgoDir'; & '$escapedPython' -m uvicorn app.main:app --host 127.0.0.1 --port $compareAlgoPort"
$compareAlgoProcess = Start-ServiceProcess -ServiceName "compare-algo" -ServiceCommand $compareAlgoCommand -LogPath $compareAlgoLogPath -PidPath $compareAlgoPidPath

$escapedAiGatewayDir = $aiGatewayDir.Replace("'", "''")
$escapedAiGatewayPython = $aiGatewayPython.Replace("'", "''")
$aiGatewayCommand = "Set-Location '$escapedAiGatewayDir'; & '$escapedAiGatewayPython' -m uvicorn app.main:app --host 127.0.0.1 --port $aiGatewayPort"
$aiGatewayProcess = Start-ServiceProcess -ServiceName "ai-gateway" -ServiceCommand $aiGatewayCommand -LogPath $aiGatewayLogPath -PidPath $aiGatewayPidPath

$escapedAuthProject = $authProject.Replace("'", "''")
$authCommand = "`$env:PATH=`"`$env:LOCALAPPDATA\Microsoft\dotnet;`$env:PATH`"; dotnet run --project '$escapedAuthProject' --launch-profile 'https'"
$authProcess = Start-ServiceProcess -ServiceName "Auth" -ServiceCommand $authCommand -LogPath $authLogPath -PidPath $authPidPath

$escapedProject = $backendProject.Replace("'", "''")
$backendCommand = "`$env:PATH=`"`$env:LOCALAPPDATA\Microsoft\dotnet;`$env:PATH`"; dotnet run --project '$escapedProject' --launch-profile 'DredgeAI.BidCompare.Host'"
$backendProcess = Start-ServiceProcess -ServiceName "Backend" -ServiceCommand $backendCommand -LogPath $backendLogPath -PidPath $backendPidPath

$escapedFrontendDir = $frontendDir.Replace("'", "''")
$frontendCommand = "Set-Location '$escapedFrontendDir'; pnpm dev"
$frontendProcess = Start-ServiceProcess -ServiceName "Frontend" -ServiceCommand $frontendCommand -LogPath $frontendLogPath -PidPath $frontendPidPath

$escapedAdminDir = $adminDir.Replace("'", "''")
$adminCommand = "Set-Location '$escapedAdminDir'; pnpm dev"
$adminProcess = Start-ServiceProcess -ServiceName "Admin Web" -ServiceCommand $adminCommand -LogPath $adminLogPath -PidPath $adminPidPath

Write-Host "      Logs: $logsDir" -ForegroundColor DarkGray
Write-Host "      Auth PID: $($authProcess.Id), Backend PID: $($backendProcess.Id), compare-algo PID: $($compareAlgoProcess.Id), ai-gateway PID: $($aiGatewayProcess.Id), frontend PID: $($frontendProcess.Id), admin-web PID: $($adminProcess.Id)" -ForegroundColor DarkGray

# 5. 健康检查
Write-Host "[5/5] Waiting for services..." -ForegroundColor Yellow
$compareAlgoHealthy = Test-HttpHealth -Label "compare-algo" -Url "$compareAlgoUrl/healthz" -TimeoutSeconds 60
$aiGatewayHealthy = Test-HttpHealth -Label "ai-gateway" -Url "$aiGatewayUrl/healthz" -TimeoutSeconds 60
$authHealthy = Test-HttpHealth -Label "Auth" -Url "$authUrl/health" -TimeoutSeconds 180
$backendHealthy = Test-HttpHealth -Label "Backend" -Url "$backendUrl/swagger/v1/swagger.json" -TimeoutSeconds 180
$frontendHealthy = Test-HttpHealth -Label "Frontend" -Url $frontendUrl -TimeoutSeconds 60
$adminHealthy = Test-HttpHealth -Label "Admin Web" -Url $adminUrl -TimeoutSeconds 60

# AnGIneer 检测（docs-api 端口 8790，不属于 DredgeAI 仓库，仅提示）
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
Write-Host ("  ai-gateway   {0}" -f $(if ($aiGatewayHealthy) { "OK" } else { "FAILED" })) -ForegroundColor $(if ($aiGatewayHealthy) { "Green" } else { "Red" })
Write-Host ("  Auth         {0}" -f $(if ($authHealthy) { "OK" } else { "FAILED" })) -ForegroundColor $(if ($authHealthy) { "Green" } else { "Red" })
Write-Host ("  Backend      {0}" -f $(if ($backendHealthy) { "OK" } else { "FAILED" })) -ForegroundColor $(if ($backendHealthy) { "Green" } else { "Red" })
Write-Host ("  Frontend     {0}" -f $(if ($frontendHealthy) { "OK" } else { "FAILED" })) -ForegroundColor $(if ($frontendHealthy) { "Green" } else { "Red" })
Write-Host ("  Admin Web    {0}" -f $(if ($adminHealthy) { "OK" } else { "FAILED" })) -ForegroundColor $(if ($adminHealthy) { "Green" } else { "Red" })
Write-Host ("  AnGIneer     {0}" -f $(if ($angineerReady) { "OK" } else { "not running" })) -ForegroundColor $(if ($angineerReady) { "Green" } else { "DarkYellow" })
Write-Host ""
Write-Host "  Frontend: $frontendUrl" -ForegroundColor Cyan
Write-Host "  Admin Web: $adminUrl" -ForegroundColor Cyan
Write-Host "  Auth: $authUrl" -ForegroundColor Cyan
Write-Host "  Backend Swagger: $backendUrl/swagger" -ForegroundColor Cyan
Write-Host "  Logs: $logsDir" -ForegroundColor DarkGray
Write-Host "  Tail logs with: .\start.ps1 -TailLogs" -ForegroundColor DarkGray

# 默认不自动打开浏览器；需要时显式加 -OpenBrowser（-NoBrowser 仍可强制关闭）
if ($backendHealthy -and $OpenBrowser -and -not $NoBrowser) {
    Start-Process $frontendUrl
}

if ($adminHealthy -and $OpenBrowser -and -not $NoBrowser) {
    Start-Process $adminUrl
}
