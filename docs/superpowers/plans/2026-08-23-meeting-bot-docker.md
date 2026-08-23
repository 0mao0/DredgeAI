# meeting-bot 模型容器化（模拟 DGX 统一模型服务）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 FireRedASR / FireRedTTS / InsightFace / YOLO 四个模型服务容器化，在本机 Docker 跑通全部 API，验证与 DGX"模型只出 API"一致的服务形态。

**Architecture:** 复用 `services/meeting-bot` 的引擎与路由代码构建一个模型服务镜像；模型权重不烧入镜像，运行时把宿主 `D:/AI/AImodles/models` 挂载到容器 `/app/models`；TTS 以镜像内 Python 3.10 子进程 worker 常驻（与 Windows 现状一致）；docker-compose 配 GPU 与端口；pytest 冒烟测试直接打到容器端口。

**Tech Stack:** Docker Desktop（WSL2 + NVIDIA Container Toolkit）、Docker Compose v2、Python 3.12（主环境）+ 3.10（TTS worker）、FastAPI/uvicorn、torch 2.9.1(cu126)/2.3.1(cu121)、FireRedASR、FireRedTTS、InsightFace、YOLOv8、pytest/httpx。

---

## 背景与前置（2026-08-23 已完成，不要重做）

- 四个模型权重已从仓库迁出到统一模型仓库 `D:/AI/AImodles/models/`：
  `fireredasr-aed-l/`（4.36GB）、`fireredtts/`（2.88GB）、`buffalo_l/`（0.32GB，位于 `models/` 子目录以兼容 insightface 的 `root/models/<name>` 约定）、`yolov8n.pt`、`faces.json`。
- 本机 `.env`（gitignore）已指向 `MODEL_DIR=D:/AI/AImodles/models`，四引擎全真实；meeting-bot 重启后四 API 已回归通过（count=2 / 人脸 1.0 / ASR 转写 / TTS WAV）。
- `scripts/deploy-meeting-bot.ps1` 增加 `-ModelDir` 参数（默认新路径），`services/meeting-bot/.env.example` 同步更新；已提交 `7f50489` 并推送 origin/github。
- Docker Desktop WSL2 已验证：`docker run --rm --gpus all nvidia/cuda:12.4.1-base-ubuntu22.04 nvidia-smi` 能看到 RTX 4070（驱动 561 / CUDA 12.6）。
- 本机 GPU 8GB，ASR(auto→GPU)+TTS(GPU) 实测占用约 7.9GB（97%）；容器测试前必须先停掉裸跑的 meeting-bot。

## 文件结构

| 文件 | 责任 |
|------|------|
| `services/meeting-bot/Dockerfile`（新建） | 主环境依赖 + FireRedTTS 3.10 环境 + 启动命令，模型只挂载不烧入 |
| `services/meeting-bot/.dockerignore`（新建） | 排除 venv/models/.scratch/tests，控制构建上下文 |
| `services/meeting-bot/docker-compose.yml`（新建） | GPU、端口 8101、模型卷、引擎环境变量 |
| `services/meeting-bot/tests/test_firered_tts_paths.py`（新建） | TTS venv python 候选路径单测 |
| `services/meeting-bot/tests/test_container_smoke.py`（新建） | 对容器端口的全 API 冒烟（health/count/asr/tts/recognize） |
| `services/meeting-bot/app/engines/firered_tts.py`（修改） | venv python 候选路径兼容 Linux `bin/python` |
| `docs/meeting-bot-deploy.md`（修改） | 新增"容器化运行（模拟 DGX）"章节 |

---

### Task 1: TTS venv python 路径兼容 Linux（TDD）

**Files:**
- Modify: `services/meeting-bot/app/engines/firered_tts.py:35-40`
- Test: `services/meeting-bot/tests/test_firered_tts_paths.py`

现状 `firered_tts.py` 只探测 Windows 布局 `.venv-tts/Scripts/python.exe`，容器（Linux）里是 `.venv-tts/bin/python`，不修则 TTS worker 找不到解释器。

- [ ] **Step 1: 写失败测试**

```python
"""FireRedTTS worker python 候选路径：Windows 与 Linux 布局都要支持。"""

from app.engines.firered_tts import _venv_python_candidates


def test_windows_layout():
    candidates = _venv_python_candidates(r"D:\svc")
    assert r"D:\svc\.venv-tts\Scripts\python.exe" in candidates


def test_linux_layout():
    candidates = _venv_python_candidates("/app")
    assert "/app/.venv-tts/bin/python" in candidates
```

- [ ] **Step 2: 运行测试确认失败**

Run（工作目录 `services/meeting-bot`）:
```powershell
uv run pytest tests/test_firered_tts_paths.py -v
```
Expected: `ModuleNotFoundError: No module named 'app.engines.firered_tts'`（或 ImportError），因为 `_venv_python_candidates` 尚不存在。

- [ ] **Step 3: 实现**

在 `services/meeting-bot/app/engines/firered_tts.py` 顶部（`class FireRedTtsEngine` 之前）加入：

```python
def _venv_python_candidates(service_root: str) -> list[str]:
    """Windows 布局 Scripts/python.exe，Linux/macOS 布局 bin/python。"""
    return [
        os.path.join(service_root, ".venv-tts", "Scripts", "python.exe"),
        os.path.join(service_root, ".venv-tts", "bin", "python"),
    ]
```

把 `__init__` 中的探测逻辑替换为：

```python
        if not venv_python:
            for candidate in _venv_python_candidates(service_root):
                if os.path.exists(candidate):
                    self._venv_python = candidate
                    break
```

- [ ] **Step 4: 运行测试确认通过**

Run: `uv run pytest tests/test_firered_tts_paths.py -v`
Expected: `2 passed`

- [ ] **Step 5: 提交**

```powershell
git add services/meeting-bot/app/engines/firered_tts.py services/meeting-bot/tests/test_firered_tts_paths.py
git commit -m "fix: FireRedTTS worker python 路径兼容 Linux bin/python（容器/ DGX 准备）"
```

---

### Task 2: Dockerfile 与 .dockerignore

**Files:**
- Create: `services/meeting-bot/Dockerfile`
- Create: `services/meeting-bot/.dockerignore`

构建前置：`services/meeting-bot/third_party/FireRedTTS` 必须在本地（gitignore，由 `scripts/deploy-meeting-bot.ps1` 第 3 步下载重组）；模型权重构建期不需要（运行时挂载）。

- [ ] **Step 1: 写 .dockerignore**

```dockerignore
.venv
.venv-tts
models
.scratch
tests
__pycache__
*.pyc
.pytest_cache
.env
```

- [ ] **Step 2: 写 Dockerfile**

```dockerfile
# AI 晨会模型服务镜像（FireRedASR / FireRedTTS / InsightFace / YOLO）
# 模型权重不烧入镜像：运行时挂载 D:/AI/AImodles/models -> /app/models
# 构建前置：third_party/FireRedTTS 已在本地（deploy-meeting-bot.ps1 第 3 步）
FROM nvidia/cuda:12.6.2-cudnn-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

RUN apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates git ffmpeg libgl1 libglib2.0-0 libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && curl -LsSf https://astral.sh/uv/install.sh | sh

WORKDIR /app

# 主环境依赖：torch 2.9.1(cu126)、fireredasr、insightface、ultralytics 等
COPY pyproject.toml uv.lock ./
RUN uv sync --group models --no-dev

# 应用代码 + FireRedTTS 源码（本地已重组并打补丁）
COPY app ./app
COPY third_party ./third_party

# FireRedTTS 专用 Python 3.10 环境（镜像内双解释器，worker 常驻子进程）
RUN uv venv --python 3.10 .venv-tts \
    && uv pip install --python .venv-tts torch==2.3.1 torchaudio==2.3.1 \
        --index-url https://download.pytorch.org/whl/cu121 \
    && uv pip install --python .venv-tts -e third_party/FireRedTTS \
    && uv pip install --python .venv-tts \
        "diffusers==0.27.2" "librosa==0.10.2" "soundfile==0.12.1" "einops==0.8.0" \
        "transformers==4.44.2" "tiktoken==0.7.0" "inflect==7.4.0" \
        "lingua-language-detector==2.0.2" "sentencex==0.6.1" \
        "huggingface-hub==0.25.2" "numpy<2"

ENV MODEL_DIR=/app/models \
    TTS_VENV_PYTHON=/app/.venv-tts/bin/python

EXPOSE 8101
CMD [".venv/bin/python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8101"]
```

- [ ] **Step 3: 构建镜像**

```powershell
cd D:\AI\DredgeAI\services\meeting-bot
docker build -t meeting-bot:dev .
```
Expected: 结尾 `Successfully tagged meeting-bot:dev`。若 `fireredasr` 无 Linux wheel 导致 `uv sync` 失败，改用本地 `pip install .scratch/fireredasr.whl`（该文件存在）并记录于 commit message。

- [ ] **Step 4: 提交**

```powershell
git add services/meeting-bot/Dockerfile services/meeting-bot/.dockerignore
git commit -m "feat: meeting-bot 模型服务 Dockerfile（模型卷挂载 + TTS 3.10 worker）"
```

---

### Task 3: docker-compose 编排

**Files:**
- Create: `services/meeting-bot/docker-compose.yml`

- [ ] **Step 1: 写 compose 文件**

```yaml
# AI 晨会模型服务容器（模拟 DGX 统一模型服务场景）
# 前置：模型权重位于宿主 D:/AI/AImodles/models；Docker Desktop 已启用 NVIDIA runtime
services:
  meeting-bot:
    build: .
    image: meeting-bot:dev
    container_name: meeting-bot
    ports:
      - "8101:8101"
    environment:
      MEETING_BOT_KEY: dev-key
      ASR_ENGINE: firered
      TTS_ENGINE: firered
      FACE_ENGINE: insightface
      COUNT_ENGINE: yolo
      MODEL_DIR: /app/models
      ASR_DEVICE: auto
      TTS_DEVICE: auto
      FACE_PROVIDERS: cpu
      COUNT_DEVICE: cpu
    volumes:
      - D:/AI/AImodles/models:/app/models
    gpus: all
    restart: unless-stopped
```

- [ ] **Step 2: 校验配置**

```powershell
docker compose -f services\meeting-bot\docker-compose.yml config --quiet
```
Expected: 无输出、退出码 0。

- [ ] **Step 3: 提交**

```powershell
git add services/meeting-bot/docker-compose.yml
git commit -m "feat: meeting-bot docker-compose（GPU + 模型卷 + 全引擎真实）"
```

---

### Task 4: 容器冒烟测试（TDD）

**Files:**
- Test: `services/meeting-bot/tests/test_container_smoke.py`

- [ ] **Step 1: 写冒烟测试**

```python
"""容器化模型服务冒烟：对运行中的 meeting-bot（裸进程或容器）做全 API 回归。

运行（容器场景）：
    $env:MEETING_BOT_BASE_URL="http://localhost:8101"
    uv run pytest tests/test_container_smoke.py -v
依赖宿主样例照片 data/meeting-bot/samples/meeting.jpg（缺失时视觉用例跳过）。
"""

import io
import os
import wave

import httpx
import numpy as np
import pytest

BASE = os.environ.get("MEETING_BOT_BASE_URL", "http://localhost:8101").rstrip("/")
KEY = os.environ.get("MEETING_BOT_KEY", "dev-key")
HEADERS = {"X-Meeting-Bot-Key": KEY}

pytestmark = pytest.mark.skipif(
    not os.environ.get("MEETING_BOT_BASE_URL"),
    reason="设置 MEETING_BOT_BASE_URL 指向被测服务",
)


def _sample_image() -> bytes | None:
    p = os.path.abspath(
        os.path.join(os.path.dirname(__file__), "..", "..", "..", "data", "meeting-bot", "samples", "meeting.jpg")
    )
    return open(p, "rb").read() if os.path.exists(p) else None


def _tone_wav(seconds: float = 2.0) -> bytes:
    rate = 16000
    n = int(rate * seconds)
    t = np.linspace(0, seconds, n, endpoint=False)
    pcm = (0.2 * np.sin(2 * np.pi * 440 * t) * 32767).astype(np.int16)
    buf = io.BytesIO()
    with wave.open(buf, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(rate)
        wf.writeframes(pcm.tobytes())
    return buf.getvalue()


def test_health():
    r = httpx.get(f"{BASE}/health", headers=HEADERS, timeout=10)
    assert r.status_code == 200
    assert r.json()["status"] == "ok"


def test_count():
    img = _sample_image()
    if img is None:
        pytest.skip("缺少样例照片 data/meeting-bot/samples/meeting.jpg")
    r = httpx.post(
        f"{BASE}/count", headers=HEADERS, files={"image": ("m.jpg", img, "image/jpeg")}, timeout=120
    )
    assert r.status_code == 200
    assert r.json()["count"] >= 1


def test_asr():
    r = httpx.post(
        f"{BASE}/asr", headers=HEADERS, files={"audio": ("t.wav", _tone_wav(), "audio/wav")}, timeout=300
    )
    assert r.status_code == 200
    assert isinstance(r.json()["text"], str)


def test_tts():
    r = httpx.post(f"{BASE}/tts", headers=HEADERS, json={"text": "今天的安全交底重点有三条。"}, timeout=300)
    assert r.status_code == 200
    assert r.content[:4] == b"RIFF"
    assert len(r.content) > 1024


def test_recognize():
    img = _sample_image()
    if img is None:
        pytest.skip("缺少样例照片 data/meeting-bot/samples/meeting.jpg")
    r = httpx.post(
        f"{BASE}/recognize", headers=HEADERS, files={"image": ("m.jpg", img, "image/jpeg")}, timeout=120
    )
    assert r.status_code == 200
    assert isinstance(r.json()["faces"], list)
```

- [ ] **Step 2: 先对当前裸服务跑通（基线）**

```powershell
cd D:\AI\DredgeAI\services\meeting-bot
$env:MEETING_BOT_BASE_URL="http://localhost:8101"
uv run pytest tests/test_container_smoke.py -v
```
Expected: `5 passed`（当前裸服务正在跑，首调含模型加载，单条最长约 60s）。

- [ ] **Step 3: 提交**

```powershell
git add services/meeting-bot/tests/test_container_smoke.py
git commit -m "test: meeting-bot 容器冒烟测试（health/count/asr/tts/recognize）"
```

---

### Task 5: 启动容器并全链路验证

**Files:** 无（运行期操作）

- [ ] **Step 1: 停掉裸跑 meeting-bot（端口 8101 冲突 + 释放显存）**

```powershell
Get-CimInstance Win32_Process -Filter "Name='python.exe'" |
  Where-Object { $_.CommandLine -match '8101|tts_worker' } |
  ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
Start-Sleep -Seconds 2
```
Expected: 8101 无监听（`Get-NetTCPConnection -LocalPort 8101` 为空）。

- [ ] **Step 2: 构建并启动容器**

```powershell
cd D:\AI\DredgeAI\services\meeting-bot
docker compose up -d --build
```
Expected: 容器 `meeting-bot` 处于 Up；启动含 InsightFace 初始化，首次就绪约 10-60s。

- [ ] **Step 3: 等待健康**

```powershell
$ok = $false
for ($i = 0; $i -lt 40; $i++) {
  try {
    $r = Invoke-RestMethod -Uri 'http://localhost:8101/health' -Headers @{'X-Meeting-Bot-Key'='dev-key'} -TimeoutSec 5
    Write-Output "HEALTH OK: $($r.status)"; $ok = $true; break
  } catch { Start-Sleep -Seconds 10 }
}
if (-not $ok) { docker logs meeting-bot --tail 30 }
```
Expected: `HEALTH OK: ok`。

- [ ] **Step 4: 容器冒烟测试**

```powershell
$env:MEETING_BOT_BASE_URL="http://localhost:8101"
uv run pytest tests/test_container_smoke.py -v
```
Expected: `5 passed`（首次 ASR 加载约 20s、TTS worker 拉起约 40s）。

- [ ] **Step 5: 人工抽查四个 API 与显存**

用 meeting-bot venv 的 python（httpx 已装）抽查，命令逐条执行：

```powershell
cd D:\AI\DredgeAI\services\meeting-bot

# 人数：期望 {"count":2}
.\.venv\Scripts\python.exe -c "import httpx; r=httpx.post('http://localhost:8101/count', headers={'X-Meeting-Bot-Key':'dev-key'}, files={'image':('m.jpg', open(r'D:\AI\DredgeAI\data\meeting-bot\samples\meeting.jpg','rb'),'image/jpeg')}, timeout=120); print(r.status_code, r.text)"

# 人脸：期望 faces 数组（含已注册工人 confidence 1.0）
.\.venv\Scripts\python.exe -c "import httpx; r=httpx.post('http://localhost:8101/recognize', headers={'X-Meeting-Bot-Key':'dev-key'}, files={'image':('m.jpg', open(r'D:\AI\DredgeAI\data\meeting-bot\samples\meeting.jpg','rb'),'image/jpeg')}, timeout=120); print(r.status_code, r.text[:160])"

# TTS：期望 200 + audio/wav（首次含 worker 拉起约 40s）
.\.venv\Scripts\python.exe -c "import httpx; r=httpx.post('http://localhost:8101/tts', headers={'X-Meeting-Bot-Key':'dev-key'}, json={'text':'今天的安全交底重点有三条。'}, timeout=300); print(r.status_code, r.headers.get('content-type'), len(r.content))"

# 容器 GPU 进程显存（宿主侧观察容器 PID，nvidia-smi 用完整路径）
& 'C:\Windows\System32\nvidia-smi.exe' --query-compute-apps=pid,used_memory --format=csv
```
Expected: count=2；人脸命中；ASR(≈3GB)+TTS(≈4.9GB) 合计 ≈7.9GB/8GB；若 OOM，把 compose 的 `ASR_DEVICE` 改为 `cpu` 后 `docker compose up -d`。

- [ ] **Step 6: 验证模型卷与 faces.json 持久化**

```powershell
docker exec meeting-bot ls /app/models
docker exec meeting-bot cat /app/models/faces.json | Select-Object -First 1
```
Expected: 四个模型目录可见；faces.json 与宿主 `D:/AI/AImodles/models/faces.json` 一致（同一文件，注册数据不丢）。

- [ ] **Step 7: 决策点——保留容器为开发模型服务**

后续日常开发直接 `docker compose up -d` 即可，不再裸跑。若需回退裸进程：`docker compose down`，再跑 `scripts\deploy-meeting-bot.ps1 -SkipModels`（已默认新模型目录）。

---

### Task 6: 文档更新与收尾提交

**Files:**
- Modify: `docs/meeting-bot-deploy.md`

- [ ] **Step 1: 追加容器化章节**

在 `docs/meeting-bot-deploy.md` 末尾追加：

```markdown
## 八、容器化运行（模拟 DGX 统一模型服务，2026-08-23）

- 模型权重统一存放 `D:/AI/AImodles/models`（仓库外），容器挂载为 `/app/models`。
- Dockerfile / docker-compose 见 `services/meeting-bot/`。构建前置：`third_party/FireRedTTS` 已在本地。
- 启动：`cd services/meeting-bot; docker compose up -d --build`（GPU 全部；ASR+TTS 合计约 7.9GB 显存，8GB 卡接近满载）。
- 冒烟：`$env:MEETING_BOT_BASE_URL="http://localhost:8101"; uv run pytest tests/test_container_smoke.py -v`。
- DGX 迁移：本机 x86_64 镜像不可直接运行于 ARM64 DGX Spark；届时用 `docker buildx --platform linux/arm64` 重建或改用 NVIDIA NIM。容器拓扑、环境变量、挂载结构在 DGX 上原样复用。
```

- [ ] **Step 2: 提交并推送两个远程**

```powershell
git add docs/meeting-bot-deploy.md
git commit -m "docs: meeting-bot 容器化运行（模拟 DGX 统一模型服务）"
git push origin master
git push github master
```
Expected: 两个远程均 `master -> master`。

---

### Task 7（后续，不占本次）: DGX arm64 镜像

本机验证的是"容器拓扑 + API 形态"，不代表镜像可直接上 DGX（DGX Spark 为 ARM64）。后续单独计划：
- `docker buildx build --platform linux/arm64` 重建镜像（torch/fireredasr/onnxruntime 需有 arm64 wheel；设计文档已列 ARM 兼容性 PoC 风险）。
- 或 DGX 上直接使用 NVIDIA NIM（Qwen）+ 自建 ASR/TTS/人脸容器。
- TTS 可在 Linux 容器升级 1S（fairseq/pynini 可编译），权重与补丁已在 `models/` 与 `patches/`。

---

## 风险与约束

| 风险 | 应对 |
|------|------|
| 8GB 显存：ASR(auto→GPU)+TTS(GPU)≈7.9GB，接近满载 | 测试前停裸进程；OOM 时 compose 里 `ASR_DEVICE=cpu` |
| `fireredasr` 无 Linux wheel | 用本地 `.scratch/fireredasr.whl` 兜底安装 |
| `third_party/FireRedTTS` 被 gitignore，新机器没有 | 先跑 `deploy-meeting-bot.ps1`（第 3 步）或从 DGX 侧带源码 |
| 构建需访问 download.pytorch.org / astral.sh | 本机已验证可直连；失败则复用宿主 pip 缓存挂载 |
| x86_64 与 ARM64 不互通 | 模拟拓扑，不模拟字节级可移植性；DGX 用 buildx/NIM |
| faces.json 在模型卷上可写 | 模拟期可接受；生产迁 PostgreSQL（WorkerProfile embedding） |
