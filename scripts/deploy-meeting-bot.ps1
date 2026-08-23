param(
    [switch]$SkipModels,
    [switch]$SkipStart
)

# AI 晨会 meeting-bot 一键部署：依赖安装 + 四个模型下载 + 启动服务（默认端口 8101）
$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$svc = Join-Path $root "services\meeting-bot"
$models = Join-Path $svc "models"
Set-Location $svc

Write-Host "[1/6] meeting-bot 依赖（Python 3.12 + 模型组）" -ForegroundColor Cyan
uv sync --group models

Write-Host "[2/6] FireRedTTS Python 3.10 环境" -ForegroundColor Cyan
if (-not (Test-Path ".venv-tts\Scripts\python.exe")) {
    uv venv --python 3.10 .venv-tts
    uv pip install --python .venv-tts torch==2.3.1 torchaudio==2.3.1 --index-url https://download.pytorch.org/whl/cu121
}

Write-Host "[3/6] FireRedTTS 源码（third_party/FireRedTTS）" -ForegroundColor Cyan
if (-not (Test-Path "third_party\FireRedTTS\fireredtts")) {
    New-Item -ItemType Directory -Force -Path ".scratch", "third_party" | Out-Null
    Invoke-WebRequest -Uri "https://codeload.github.com/FireRedTeam/FireRedTTS/tar.gz/refs/heads/main" `
        -OutFile ".scratch\fireredtts.tgz" -UseBasicParsing
    uv run python -c "import tarfile; tarfile.open(r'.scratch\fireredtts.tgz').extractall(r'.scratch')"
    $extracted = Get-ChildItem ".scratch" -Directory | Where-Object { $_.Name -like "FireRedTTS*" } | Select-Object -First 1
    Move-Item $extracted.FullName "third_party\FireRedTTS"
    New-Item -ItemType File -Force -Path "third_party\FireRedTTS\fireredtts\__init__.py" | Out-Null
    git apply --directory="third_party\FireRedTTS" "patches\fireredtts-windows.patch"
}
if (-not (Test-Path "third_party\FireRedTTS\fireredtts.egg-info")) {
    uv pip install --python .venv-tts -e "third_party\FireRedTTS"
    # pynini（WeTextProcessing）无 Windows wheel，正常化已做免依赖降级，跳过
    uv pip install --python .venv-tts `
        "diffusers==0.27.2" "librosa==0.10.2" "soundfile==0.12.1" "einops==0.8.0" `
        "transformers==4.44.2" "tiktoken==0.7.0" "inflect==7.4.0" `
        "lingua-language-detector==2.0.2" "sentencex==0.6.1" `
        "huggingface-hub==0.25.2" "numpy<2"
}

if (-not $SkipModels) {
    Write-Host "[4/6] YOLO + InsightFace 权重" -ForegroundColor Cyan
    New-Item -ItemType Directory -Force -Path $models | Out-Null
    if (-not (Test-Path "$models\yolov8n.pt")) {
        Invoke-WebRequest -Uri "https://github.com/ultralytics/assets/releases/download/v8.3.0/yolov8n.pt" `
            -OutFile "$models\yolov8n.pt" -UseBasicParsing
    }
    if (-not (Test-Path "$models\buffalo_l\w600k_r50.onnx")) {
        Invoke-WebRequest -Uri "https://github.com/deepinsight/insightface/releases/download/v0.7/buffalo_l.zip" `
            -OutFile "$models\buffalo_l.zip" -UseBasicParsing
        uv run python -c "import zipfile, os, shutil; p=r'$models\buffalo_l.zip'; z=zipfile.ZipFile(p); z.extractall(r'$models'); z.close(); os.remove(p)"
        New-Item -ItemType Directory -Force -Path "$models\buffalo_l" | Out-Null
        Get-ChildItem $models -File -Filter "*.onnx" | Move-Item -Destination "$models\buffalo_l\" -Force
    }

    Write-Host "[5/6] FireRedASR-AED-L（ModelScope，约 4.7GB，缓存后跳过）" -ForegroundColor Cyan
    uv run python -c "from modelscope import snapshot_download; print(snapshot_download('pengzhendong/FireRedASR-AED-L'))"

    Write-Host "[5b/6] FireRedTTS 权重（ModelScope，约 3.1GB，缓存后跳过）" -ForegroundColor Cyan
    $ttsHub = uv run python -c "from modelscope import snapshot_download; print(snapshot_download('FireRedTeam/FireRedTTS'))"
    if (-not (Test-Path "$models\fireredtts\pretrained_models\fireredtts_gpt.pt")) {
        New-Item -ItemType Directory -Force -Path "$models\fireredtts\pretrained_models" | Out-Null
        Copy-Item "$ttsHub\fireredtts_gpt.pt" "$models\fireredtts\pretrained_models\" -Force
        Copy-Item "$ttsHub\fireredtts_speaker.bin" "$models\fireredtts\pretrained_models\" -Force
        Copy-Item "$ttsHub\fireredtts_token2wav.pt" "$models\fireredtts\pretrained_models\" -Force
    }
} else {
    Write-Host "[4-5/6] 跳过模型下载（-SkipModels）" -ForegroundColor Yellow
}

if (-not $SkipStart) {
    Write-Host "[6/6] 启动 meeting-bot http://localhost:8101" -ForegroundColor Cyan
    uv run uvicorn app.main:app --host 0.0.0.0 --port 8101
} else {
    Write-Host "[6/6] 跳过启动（-SkipStart）。手动启动：cd services/meeting-bot; uv run uvicorn app.main:app --host 0.0.0.0 --port 8101" -ForegroundColor Yellow
}
