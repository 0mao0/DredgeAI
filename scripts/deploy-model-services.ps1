param(
    [switch]$SkipModels,
    [switch]$SkipStart,
    [string]$ModelDir = "D:\AI\AImodles\models",
    [string]$CosyVoiceRoot = "D:\AI\AImodles\cosyvoice"
)

# 一模型一容器：停旧裸跑 -> 下载缺失权重 -> docker compose 起五容器 -> 冒烟
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

Write-Host "[0/5] 停掉旧裸跑进程（meeting-bot/CosyVoice）" -ForegroundColor Cyan
Get-CimInstance Win32_Process -Filter "Name='python.exe'" |
    Where-Object { $_.CommandLine -match 'uvicorn app\.main:app|server\.py --port 8000' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
Start-Sleep -Seconds 2

if (-not $SkipModels) {
    Write-Host "[1/5] SenseVoice-Small 权重（ModelScope iic/SenseVoiceSmall）" -ForegroundColor Cyan
    $senseDir = Join-Path $ModelDir "SenseVoiceSmall"
    if (-not (Test-Path (Join-Path $senseDir "model.pt"))) {
        New-Item -ItemType Directory -Force -Path $senseDir | Out-Null
        Push-Location (Join-Path $root "services\sensevoice")
        uv sync
        uv run python -c "from modelscope import snapshot_download; snapshot_download('iic/SenseVoiceSmall', local_dir=r'$senseDir')"
        Pop-Location
    } else {
        Write-Host "  已存在，跳过" -ForegroundColor Yellow
    }

    Write-Host "[2/5] CosyVoice3 权重（FunAudioLLM/Fun-CosyVoice3-0.5B-2512）" -ForegroundColor Cyan
    $cosyModel = Join-Path $CosyVoiceRoot "pretrained_models\Fun-CosyVoice3-0.5B"
    if (-not (Test-Path (Join-Path $cosyModel "cosyvoice3.yaml"))) {
        New-Item -ItemType Directory -Force -Path $cosyModel | Out-Null
        Push-Location (Join-Path $root "services\sensevoice")
        uv run --with modelscope python -c "from modelscope import snapshot_download; snapshot_download('FunAudioLLM/Fun-CosyVoice3-0.5B-2512', local_dir=r'$cosyModel')"
        Pop-Location
    } else {
        Write-Host "  已存在，跳过" -ForegroundColor Yellow
    }

    Write-Host "[3/5] 人脸/人数权重检查" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $ModelDir | Out-Null
    if (-not (Test-Path (Join-Path $ModelDir "yolov8n.pt"))) {
        Invoke-WebRequest -Uri "https://github.com/ultralytics/assets/releases/download/v8.3.0/yolov8n.pt" -OutFile (Join-Path $ModelDir "yolov8n.pt") -UseBasicParsing
    }
    if (-not (Test-Path (Join-Path $ModelDir "buffalo_l\w600k_r50.onnx"))) {
        throw "缺少 buffalo_l 人脸权重：请先用旧脚本 scripts\deploy-meeting-bot.ps1 -SkipStart 补齐"
    }
} else {
    Write-Host "[1-3/5] 跳过权重下载（-SkipModels）" -ForegroundColor Yellow
}

if (-not $SkipStart) {
    Write-Host "[4/5] 构建并启动五容器" -ForegroundColor Cyan
    docker compose -f services\meeting-bot\docker-compose.yml up -d --build

    Write-Host "[5/5] 等待健康" -ForegroundColor Cyan
    $targets = @(
        @{ Name = "meeting-bot"; Url = "http://localhost:8101/health" },
        @{ Name = "sensevoice"; Url = "http://localhost:8102/health" },
        @{ Name = "cosyvoice"; Url = "http://localhost:8000/api/health" },
        @{ Name = "insightface"; Url = "http://localhost:8103/health" },
        @{ Name = "yolo"; Url = "http://localhost:8104/health" }
    )
    foreach ($t in $targets) {
        $ok = $false
        for ($i = 0; $i -lt 60; $i++) {
            try {
                $r = Invoke-RestMethod -Uri $t.Url -Headers @{'X-Meeting-Bot-Key'='dev-key'} -TimeoutSec 5
                if ($t.Name -eq "cosyvoice") {
                    if ($r.model_loaded) { $ok = $true; break }
                } else {
                    $ok = $true; break
                }
            } catch {}
            Start-Sleep -Seconds 5
        }
        if (-not $ok) {
            Write-Host "  $($t.Name) 未就绪，查看日志:" -ForegroundColor Red
            docker logs $t.Name --tail 30
            throw "$($t.Name) health check 超时"
        }
        Write-Host "  $($t.Name) OK" -ForegroundColor Green
    }
    Write-Host "全部就绪：http://localhost:8101（meeting-bot 聚合层）" -ForegroundColor Cyan
} else {
    Write-Host "[4-5/5] 跳过启动（-SkipStart）。手动启动：docker compose -f services\meeting-bot\docker-compose.yml up -d --build" -ForegroundColor Yellow
}
