# AI 晨会模型服务「一模型一容器」实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 AI 晨会模型服务拆成「一模型一容器」（SenseVoice-Small ASR、CosyVoice3-0.5B TTS、InsightFace 人脸、YOLO 人数）+ meeting-bot 聚合层，模型权重统一放 `D:\AI\AImodles` 挂载进容器，对外 :8101 API 不变，为 DGX Spark 迁移铺路。

**Architecture:** 五个独立 FastAPI 容器走同一 compose 网络：`sensevoice:8102`、`cosyvoice:8000`、`insightface:8103`、`yolo:8104` 各持一个模型，`meeting-bot:8101` 只做 HTTP 转发与长音频后台任务；所有服务共享 `MEETING_BOT_KEY`（默认 dev-key）鉴权，模型权重只挂载不烧入镜像。

**Tech Stack:** Docker Compose v2、Python 3.12（sensevoice/insightface/yolo/meeting-bot）+ 3.10（cosyvoice）、FastAPI/uvicorn、funasr（SenseVoice-Small）、CosyVoice3（Fun-CosyVoice3-0.5B-2512）、InsightFace buffalo_l、YOLOv8n、httpx、pytest。

---

## 文件结构

| 文件 | 责任 |
|------|------|
| `services/sensevoice/pyproject.toml` | 依赖：fastapi + funasr + torch 2.9.1(cu126) |
| `services/sensevoice/app/{main,settings,security}.py` | 入口/配置/鉴权 |
| `services/sensevoice/app/routes/asr.py` | POST /asr |
| `services/sensevoice/app/engines/{audio,sensevoice_asr}.py` | 音频工具 + SenseVoice 引擎（audio.py 从 meeting-bot 迁入） |
| `services/sensevoice/{Dockerfile,.dockerignore,tests,uv.lock,.env.example}` | 容器/测试 |
| `services/cosyvoice/{server.py,voices_config.json}` | 从 `D:\AI\AImodles\cosyvoice` 复制并改造（env 路径 + 鉴权 + 默认音色） |
| `services/cosyvoice/third_party/CosyVoice/` | CosyVoice 源码（gitignore，本地 clone 复制，含 Matcha-TTS） |
| `services/cosyvoice/{pyproject.toml,Dockerfile,.dockerignore,tests,uv.lock,.env.example}` | 容器/测试 |
| `services/insightface/` | 从 meeting-bot 迁入 face 引擎/路由，新建入口/Dockerfile/测试 |
| `services/yolo/` | 从 meeting-bot 迁入 yolo 引擎/路由，新建入口/Dockerfile/测试 |
| `services/meeting-bot/app/settings.py` | 改为模型服务 URL 配置（去掉引擎枚举） |
| `services/meeting-bot/app/routes/*.py` | 全部改为 HTTP 转发；transcribe 保留后台任务 |
| `services/meeting-bot/app/engines/` | 整体删除（git 历史保留；引擎迁入对应新服务） |
| `services/meeting-bot/{pyproject.toml,Dockerfile}` | 瘦身：fastapi + httpx，python:3.12-slim |
| `services/meeting-bot/docker-compose.yml` | 五服务编排（healthcheck/GPU/数据卷） |
| `services/meeting-bot/tests/test_container_smoke.py` | 五容器全 API 冒烟（重写） |
| `services/meeting-bot/tests/test_proxy.py`、`test_transcribe.py` | 转发路由单测（mock httpx） |
| `scripts/deploy-model-services.ps1` | 权重下载 + 一键起五容器 + 冒烟 |
| `.gitignore` | 新增四服务 third_party/models/.venv/tests 忽略规则 |
| `docs/meeting-bot-deploy.md` | 新增「一模型一容器」章节 |

---

### Task 0: 预检

**Files:** 无（只读检查）

- [ ] **Step 1: 确认前置**

Run（PowerShell，仓库根 `D:\AI\DredgeAI`）：
```powershell
uv --version
docker compose version
Test-Path "D:\AI\AImodles\models\buffalo_l\w600k_r50.onnx"
Test-Path "D:\AI\AImodles\models\yolov8n.pt"
Test-Path "D:\AI\AImodles\cosyvoice\pretrained_models\Fun-CosyVoice3-0.5B\cosyvoice.yaml"
Test-Path "D:\AI\AImodles\cosyvoice\CosyVoice\third_party\Matcha-TTS"
```

Expected: uv 与 docker compose 有版本输出；四个模型路径全为 True。若 Matcha-TTS 缺失，在 `D:\AI\AImodles\cosyvoice\CosyVoice` 执行 `git submodule update --init --recursive` 后重查。

- [ ] **Step 2: 记录当前 git 状态**

Run: `git status --short`
Expected: 只有用户既有的未提交改动（tender-read 等），本次实施只新增/修改本计划列出的文件；遇到重叠时停下询问。

---

### Task 1: sensevoice 服务骨架（TDD）

**Files:**
- Create: `services/sensevoice/pyproject.toml`
- Create: `services/sensevoice/.python-version`
- Create: `services/sensevoice/.env.example`
- Create: `services/sensevoice/.dockerignore`
- Create: `services/sensevoice/app/__init__.py`
- Create: `services/sensevoice/app/routes/__init__.py`
- Create: `services/sensevoice/app/engines/__init__.py`
- Create: `services/sensevoice/app/settings.py`
- Create: `services/sensevoice/app/security.py`
- Create: `services/sensevoice/app/main.py`
- Create: `services/sensevoice/app/routes/asr.py`
- Create: `services/sensevoice/app/engines/audio.py`（从 meeting-bot 复制）
- Create: `services/sensevoice/app/engines/sensevoice_asr.py`（占位引擎，Task 2 替换为真实实现）
- Test: `services/sensevoice/tests/test_asr_route.py`

- [ ] **Step 1: 建目录并复制音频工具**

```powershell
New-Item -ItemType Directory -Force -Path "services\sensevoice\app\routes", "services\sensevoice\app\engines", "services\sensevoice\tests"
Copy-Item "services\meeting-bot\app\engines\audio.py" "services\sensevoice\app\engines\audio.py"
New-Item -ItemType File -Force -Path "services\sensevoice\app\__init__.py", "services\sensevoice\app\routes\__init__.py", "services\sensevoice\app\engines\__init__.py"
```

- [ ] **Step 2: 写 pyproject 并生成 uv.lock**

`services/sensevoice/pyproject.toml`：
```toml
[project]
name = "sensevoice"
version = "0.1.0"
description = "SenseVoice-Small ASR 模型服务"
requires-python = ">=3.12"
dependencies = [
    "fastapi>=0.115",
    "uvicorn[standard]>=0.30",
    "pydantic-settings>=2.4",
    "python-multipart>=0.0.9",
    "numpy>=1.24",
    "funasr>=1.2,<2",
    "torch==2.9.1",
    "torchaudio==2.9.1",
]

[[tool.uv.index]]
name = "pytorch-cu126"
url = "https://download.pytorch.org/whl/cu126"
explicit = true

[tool.uv.sources]
torch = { index = "pytorch-cu126" }
torchaudio = { index = "pytorch-cu126" }

[dependency-groups]
dev = ["pytest>=8", "httpx>=0.27"]

[tool.pytest.ini_options]
pythonpath = ["."]
testpaths = ["tests"]
```

`services/sensevoice/.python-version`：`3.12`

`services/sensevoice/.env.example`：
```dotenv
MEETING_BOT_KEY=dev-key
MODEL_DIR=/app/models
ASR_DEVICE=cpu
```

`services/sensevoice/.dockerignore`：
```dockerignore
.venv
models
third_party
tests
__pycache__
*.pyc
.pytest_cache
.env
```

Run（工作目录 `services/sensevoice`）：`uv lock`
Expected: 生成 `uv.lock`，无报错。

- [ ] **Step 3: 写失败的路由测试**

`services/sensevoice/tests/test_asr_route.py`：
```python
from fastapi.testclient import TestClient

from app.main import app
from app.engines.sensevoice_asr import AsrResult


class FakeAsrEngine:
    def __init__(self):
        self.last_bytes = None

    def transcribe(self, audio_bytes: bytes, sample_rate: int = 16000) -> AsrResult:
        self.last_bytes = audio_bytes
        return AsrResult(text="测试转写")

    @property
    def loaded(self) -> bool:
        return True


def test_asr_returns_text():
    with TestClient(app) as client:
        app.state.asr_engine = FakeAsrEngine()
        resp = client.post(
            "/asr",
            headers={"X-Meeting-Bot-Key": "dev-key"},
            files={"audio": ("q.wav", b"fake-wav", "audio/wav")},
        )
    assert resp.status_code == 200
    assert resp.json()["text"] == "测试转写"


def test_health_reports_loaded():
    with TestClient(app) as client:
        app.state.asr_engine = FakeAsrEngine()
        resp = client.get("/health", headers={"X-Meeting-Bot-Key": "dev-key"})
    assert resp.status_code == 200
    assert resp.json()["model_loaded"] is True
```

- [ ] **Step 4: 运行测试确认失败**

Run（工作目录 `services/sensevoice`）：`uv run pytest tests/test_asr_route.py -v`
Expected: `ModuleNotFoundError: No module named 'app'`（依赖/入口尚未建）。

- [ ] **Step 5: 写入口/配置/鉴权/路由 + 占位引擎**

`services/sensevoice/app/settings.py`：
```python
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    meeting_bot_key: str = "dev-key"
    model_dir: str = "models"
    asr_device: str = "cpu"  # cpu | cuda


settings = Settings()
```

`services/sensevoice/app/security.py`：
```python
from fastapi import HTTPException, Request

from app.settings import settings


async def require_key(request: Request):
    if request.headers.get("X-Meeting-Bot-Key") != settings.meeting_bot_key:
        raise HTTPException(status_code=401, detail="invalid key")
```

`services/sensevoice/app/main.py`：
```python
from fastapi import Depends, FastAPI

from app.security import require_key
from app.settings import settings
from app.routes.asr import router as asr_router

app = FastAPI(title="sensevoice", dependencies=[Depends(require_key)])
app.include_router(asr_router)


@app.get("/health")
def health():
    return {"status": "ok", "model_loaded": app.state.asr_engine.loaded}


@app.on_event("startup")
def startup():
    from app.engines.sensevoice_asr import SenseVoiceAsrEngine

    app.state.asr_engine = SenseVoiceAsrEngine(
        model_dir=settings.model_dir,
        device=settings.asr_device,
    )
```

`services/sensevoice/app/routes/asr.py`：
```python
from fastapi import APIRouter, File, Request, UploadFile

router = APIRouter()


@router.post("/asr")
def asr(request: Request, audio: UploadFile = File(...)):
    engine = request.app.state.asr_engine
    data = audio.file.read()
    result = engine.transcribe(data)
    return {"text": result.text}
```

`services/sensevoice/app/engines/sensevoice_asr.py`（占位，Task 2 整体替换）：
```python
"""占位引擎：Task 2 替换为真实 SenseVoice 实现。"""


class AsrResult:
    def __init__(self, text: str):
        self.text = text


class SenseVoiceAsrEngine:
    def __init__(self, model_dir: str = "models", device: str = "cpu"):
        self._model = None

    @property
    def loaded(self) -> bool:
        return self._model is not None

    def transcribe(self, audio_bytes: bytes, sample_rate: int = 16000) -> AsrResult:
        raise NotImplementedError("Task 2 实现")
```

- [ ] **Step 6: 运行测试确认通过**

Run（工作目录 `services/sensevoice`）：`uv run pytest tests/test_asr_route.py -v`
Expected: `2 passed`（FakeAsrEngine 替换了 app.state，不加载真实模型；`SenseVoiceAsrEngine` 在 startup 里被覆盖前只做轻量构造）。

- [ ] **Step 7: 提交**

```powershell
git add services/sensevoice
git commit -m "feat(sensevoice): 服务骨架（配置/入口/鉴权/路由/音频工具）"
```

---

### Task 2: sensevoice ASR 引擎（TDD）

**Files:**
- Create: `services/sensevoice/app/engines/sensevoice_asr.py`
- Test: `services/sensevoice/tests/test_sensevoice_asr.py`

- [ ] **Step 1: 写失败测试（标签剥离 + 引擎转写）**

`services/sensevoice/tests/test_sensevoice_asr.py`：
```python
import pytest

from app.engines.sensevoice_asr import SenseVoiceAsrEngine, strip_sensevoice_tags


def test_strip_tags():
    raw = "<|zh|><|NEUTRAL|><|Speech|>今天的安全交底有三条<|endoftext|>"
    assert strip_sensevoice_tags(raw) == "今天的安全交底有三条"


def test_strip_tags_empty():
    assert strip_sensevoice_tags("<|nospeech|><|zh|>") == ""


class FakeSenseVoiceModel:
    def __init__(self, texts):
        self.texts = texts
        self.calls = 0

    def generate(self, input, language="auto", use_itn=True, batch_size_s=60):
        self.calls += 1
        return [{"text": self.texts[self.calls - 1]}]


def test_transcribe_strips_and_concatenates(monkeypatch):
    engine = SenseVoiceAsrEngine(model_dir="models", device="cpu")
    fake = FakeSenseVoiceModel([
        "<|zh|><|NEUTRAL|><|Speech|>第一条内容<|endoftext|>",
        "<|zh|><|NEUTRAL|><|Speech|>第二条内容<|endoftext|>",
    ])
    engine._model = fake
    # 单测不依赖 ffmpeg：跳过转码与切块
    monkeypatch.setattr("app.engines.sensevoice_asr.to_wav_16k_mono", lambda data: data)
    monkeypatch.setattr("app.engines.sensevoice_asr.split_wav_16k_mono", lambda wav, chunk_seconds=50: [b"chunk1", b"chunk2"])
    result = engine.transcribe(b"fake-wav")
    assert result.text == "第一条内容第二条内容"
    assert fake.calls == 2


def test_transcribe_missing_model_dir_raises(monkeypatch, tmp_path):
    engine = SenseVoiceAsrEngine(model_dir=str(tmp_path), device="cpu")
    with pytest.raises(RuntimeError, match="SenseVoice-Small"):
        engine.transcribe(b"fake-wav")
```

- [ ] **Step 2: 运行测试确认失败**

Run（工作目录 `services/sensevoice`）：`uv run pytest tests/test_sensevoice_asr.py -v`
Expected: FAIL（`strip_sensevoice_tags` 未定义）。

- [ ] **Step 3: 实现引擎（整体替换 Task 1 的占位文件）**

`services/sensevoice/app/engines/sensevoice_asr.py`：
```python
"""SenseVoice-Small ASR 引擎（funasr）。"""

from __future__ import annotations

import os
import re
import tempfile
import threading

from app.engines.audio import split_wav_16k_mono, to_wav_16k_mono

_TAG_RE = re.compile(r"<\|[^|]+\|>")


def strip_sensevoice_tags(text: str) -> str:
    """去掉 SenseVoice 输出的 <|zh|><|NEUTRAL|> 等标签，保留正文。"""
    return _TAG_RE.sub("", text).strip()


class AsrResult:
    def __init__(self, text: str):
        self.text = text


class SenseVoiceAsrEngine:
    """funasr 加载 SenseVoice-Small；权重在 <model_dir>/SenseVoiceSmall。"""

    def __init__(self, model_dir: str = "models", device: str = "cpu"):
        self._model_root = os.path.join(os.path.abspath(model_dir), "SenseVoiceSmall")
        self._device = device
        self._model = None
        self._lock = threading.Lock()

    @property
    def loaded(self) -> bool:
        return self._model is not None

    def _ensure_model(self):
        if self._model is not None:
            return self._model
        with self._lock:
            if self._model is not None:
                return self._model
            if not os.path.isdir(self._model_root):
                raise RuntimeError(
                    f"缺少 SenseVoice-Small 权重（{self._model_root}），请先运行 scripts/deploy-model-services.ps1"
                )
            try:
                from funasr import AutoModel

                self._model = AutoModel(
                    model="iic/SenseVoiceSmall",
                    model_dir=self._model_root,
                    device=self._device,
                    disable_update=True,
                    disable_pbar=True,
                )
            except Exception as exc:
                raise RuntimeError("SenseVoice-Small 加载失败: " + str(exc)) from exc
            return self._model

    def transcribe(self, audio_bytes: bytes, sample_rate: int = 16000) -> AsrResult:
        model = self._ensure_model()
        wav = to_wav_16k_mono(audio_bytes)
        chunks = split_wav_16k_mono(wav, chunk_seconds=50)
        if not chunks:
            return AsrResult(text="")
        texts: list[str] = []
        with tempfile.TemporaryDirectory() as tmp:
            for idx, chunk in enumerate(chunks):
                wav_path = os.path.join(tmp, f"chunk_{idx}.wav")
                with open(wav_path, "wb") as f:
                    f.write(chunk)
                res = model.generate(
                    input=wav_path,
                    language="auto",
                    use_itn=True,
                    batch_size_s=60,
                )
                if not res:
                    continue
                text = strip_sensevoice_tags(res[0].get("text") or "")
                if text:
                    texts.append(text)
        return AsrResult(text="".join(texts))
```

- [ ] **Step 4: 运行测试确认通过**

Run（工作目录 `services/sensevoice`）：`uv run pytest tests/test_sensevoice_asr.py -v`
Expected: `4 passed`。

- [ ] **Step 5: 回归 Task 1 路由测试**

Run: `uv run pytest -v`
Expected: `6 passed`。

- [ ] **Step 6: 提交**

```powershell
git add services/sensevoice
git commit -m "feat(sensevoice): SenseVoice-Small ASR 引擎（标签剥离/切块转写）"
```

---

### Task 3: sensevoice Dockerfile + 容器冒烟

**Files:**
- Create: `services/sensevoice/Dockerfile`

- [ ] **Step 1: 写 Dockerfile**

`services/sensevoice/Dockerfile`：
```dockerfile
# SenseVoice-Small ASR 模型服务
# 权重不烧入镜像：运行时挂载 D:/AI/AImodles/models -> /app/models
FROM nvidia/cuda:12.6.2-cudnn-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

RUN apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates ffmpeg libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && curl -LsSf https://astral.sh/uv/install.sh | sh

WORKDIR /app

COPY pyproject.toml uv.lock .python-version ./
RUN uv sync --frozen --no-dev

COPY app ./app

ENV MODEL_DIR=/app/models \
    ASR_DEVICE=cpu \
    MEETING_BOT_KEY=dev-key

EXPOSE 8102
CMD [".venv/bin/python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8102"]
```

- [ ] **Step 2: 确保权重存在**

Run（工作目录 `services/sensevoice`）：
```powershell
if (-not (Test-Path "D:\AI\AImodles\models\SenseVoiceSmall\model.pt")) {
    uv sync
    uv run python -c "from funasr import AutoModel; AutoModel(model='iic/SenseVoiceSmall', model_dir=r'D:\AI\AImodles\models\SenseVoiceSmall', disable_update=True, disable_pbar=True)"
}
```
Expected: `D:\AI\AImodles\models\SenseVoiceSmall\model.pt` 存在（约 900MB，下载一次）。

- [ ] **Step 3: 构建并启动容器**

```powershell
docker build -t sensevoice:dev services\sensevoice
docker run -d --name sensevoice -p 8102:8102 -v D:/AI/AImodles/models:/app/models -e MEETING_BOT_KEY=dev-key sensevoice:dev
```
Expected: 容器 Up。

- [ ] **Step 4: 冒烟**

```powershell
$ok = $false
for ($i = 0; $i -lt 30; $i++) {
    try { $r = Invoke-RestMethod -Uri "http://localhost:8102/health" -Headers @{'X-Meeting-Bot-Key'='dev-key'} -TimeoutSec 3; $ok = $true; break } catch { Start-Sleep -Seconds 5 }
}
if (-not $ok) { docker logs sensevoice --tail 30; throw "health not ready" }
$r | ConvertTo-Json
```
Expected: `{"status":"ok","model_loaded":false}`。

- [ ] **Step 5: 停容器（留给 Task 8 compose 统一管理）**

```powershell
docker rm -f sensevoice
```

- [ ] **Step 6: 提交**

```powershell
git add services/sensevoice/Dockerfile
git commit -m "feat(sensevoice): Dockerfile（funasr + 模型卷挂载）"
```

---

### Task 4: cosyvoice 服务容器化

**Files:**
- Create: `services/cosyvoice/`（server.py、voices_config.json、pyproject.toml、uv.lock、.python-version、Dockerfile、.dockerignore、.env.example、tests/、third_party/CosyVoice 复制）

- [ ] **Step 1: 复制现有服务文件与源码**

```powershell
New-Item -ItemType Directory -Force -Path "services\cosyvoice\tests", "services\cosyvoice\third_party"
Copy-Item "D:\AI\AImodles\cosyvoice\server.py" "services\cosyvoice\server.py"
Copy-Item "D:\AI\AImodles\cosyvoice\voices_config.json" "services\cosyvoice\voices_config.json"
Copy-Item "D:\AI\AImodles\cosyvoice\CosyVoice" "services\cosyvoice\third_party\CosyVoice" -Recurse
```
Expected: `services\cosyvoice\third_party\CosyVoice\cosyvoice\cli\cosyvoice.py` 与 `services\cosyvoice\third_party\CosyVoice\third_party\Matcha-TTS` 均存在。

- [ ] **Step 2: 写 pyproject 并生成 uv.lock**

`services/cosyvoice/pyproject.toml`：
```toml
[project]
name = "cosyvoice"
version = "0.1.0"
description = "CosyVoice3-0.5B TTS 模型服务"
requires-python = ">=3.10,<3.12"
dependencies = [
    "fastapi==0.115.6",
    "uvicorn[standard]==0.30.0",
    "python-multipart>=0.0.9",
    "pydantic==2.7.0",
    "numpy==1.26.4",
    "tqdm>=4.66",
    "hyperpyyaml==1.2.3",
    "modelscope==1.20.0",
    "omegaconf==2.3.0",
    "transformers==4.51.3",
    "diffusers==0.29.0",
    "x-transformers==2.11.24",
    "einops>=0.8",
    "librosa==0.10.2",
    "soundfile==0.12.1",
    "pyworld==0.3.4",
    "inflect==7.3.1",
    "onnx==1.16.0",
    "onnxruntime==1.18.0",
    "pytorch-lightning==2.2.4",
    "torch==2.3.1",
    "torchaudio==2.3.1",
]

[[tool.uv.index]]
name = "pytorch-cu121"
url = "https://download.pytorch.org/whl/cu121"
explicit = true

[tool.uv.sources]
torch = { index = "pytorch-cu121" }
torchaudio = { index = "pytorch-cu121" }

[dependency-groups]
dev = ["pytest>=8", "pytest-asyncio>=0.23", "httpx>=0.27"]

[tool.pytest.ini_options]
pythonpath = ["."]
testpaths = ["tests"]
asyncio_mode = "auto"
```

`services/cosyvoice/.python-version`：`3.10`

`services/cosyvoice/.dockerignore`：
```dockerignore
.venv
third_party/CosyVoice/.git
third_party/CosyVoice/.github
third_party/CosyVoice/runtime
tests
__pycache__
*.pyc
.pytest_cache
.env
```

`services/cosyvoice/.env.example`：
```dotenv
MEETING_BOT_KEY=dev-key
COSYVOICE_DATA=/data
TTS_VOICE_ID=zh-male-news
```

Run（工作目录 `services/cosyvoice`）：`uv lock`
Expected: 生成 `uv.lock`。

- [ ] **Step 3: 写测试（鉴权 + 默认音色，不加载模型）**

`services/cosyvoice/tests/test_server.py`：
```python
import pytest
from fastapi import HTTPException
from starlette.requests import Request

from server import DEFAULT_VOICE_ID, require_key, _resolve_voice_id


def _req(headers=None):
    h = [(k.lower().encode(), v.encode()) for k, v in (headers or {}).items()]
    return Request({
        "type": "http", "method": "GET", "path": "/", "query_string": b"",
        "headers": h, "server": ("test", 80), "client": ("test", 80), "scheme": "http",
    })


@pytest.mark.asyncio
async def test_require_key_rejects_missing():
    with pytest.raises(HTTPException) as exc:
        await require_key(_req())
    assert exc.value.status_code == 401


@pytest.mark.asyncio
async def test_require_key_accepts_matching():
    await require_key(_req({"X-Meeting-Bot-Key": "dev-key"}))


def test_resolve_voice_falls_back_to_default():
    assert _resolve_voice_id({"zh-male-news": "x"}, "not-exist") == DEFAULT_VOICE_ID
    assert _resolve_voice_id({"zh-male-news": "x"}, "zh-male-news") == "zh-male-news"
```

- [ ] **Step 4: 运行测试确认失败**

Run: `uv run pytest tests/test_server.py -v`
Expected: `ImportError`（`require_key` / `_resolve_voice_id` 尚未定义）。

- [ ] **Step 5: 改造 server.py（env 路径 + 懒加载 + 鉴权 + 默认音色）**

用 apply_patch 修改 `services/cosyvoice/server.py`：

替换文件顶部路径与导入段：
```python
ROOT_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.environ.get('COSYVOICE_DATA', ROOT_DIR)
SRC_DIR = os.environ.get('COSYVOICE_SRC', os.path.join(ROOT_DIR, 'CosyVoice'))
COSYVOICE_DIR = os.path.join(DATA_DIR, 'CosyVoice')
ASSET_DIR = os.path.join(COSYVOICE_DIR, 'asset')
SAMPLES_DIR = os.path.join(ASSET_DIR, 'samples')
CONFIG_PATH = os.environ.get('VOICES_CONFIG', os.path.join(DATA_DIR, 'voices_config.json'))
SAMPLE_TEXT = '你好，欢迎试听我的声音，希望你喜欢。'
MODEL_DIR = os.environ.get('COSYVOICE_MODEL_DIR', os.path.join(DATA_DIR, 'pretrained_models', 'Fun-CosyVoice3-0.5B'))
PROMPT_WAV_PATH = os.path.join(ASSET_DIR, '男声-播报_converted_norm.wav')
DEFAULT_VOICE_ID = os.environ.get('TTS_VOICE_ID', 'zh-male-news')

sys.path.insert(0, SRC_DIR)
sys.path.insert(0, os.path.join(SRC_DIR, 'third_party', 'Matcha-TTS'))
```

删除原来的顶层 `from cosyvoice.cli.cosyvoice import AutoModel`，把导入移进 `_load_model()`：
```python
def _load_model():
    global cosyvoice, _model_loaded, _model_error
    try:
        from cosyvoice.cli.cosyvoice import AutoModel
        print(f'[startup] Loading CosyVoice3 from {MODEL_DIR} ...', flush=True)
        cosyvoice = AutoModel(model_dir=MODEL_DIR)
        _model_loaded = True
        print('[startup] Model loaded OK', flush=True)
    except Exception as e:
        _model_error = repr(e)
        print('[startup] Model load FAILED:', e, flush=True)
```

把 `VOICE_WAVS[vid]` 的 ROOT_DIR 拼接改为 DATA_DIR（`_load_config` 后的构造循环与 `_reload_voices` 两处）：
```python
VOICE_WAVS[vid] = os.path.join(DATA_DIR, v['wav']) if not os.path.isabs(v['wav']) else v['wav']
```

鉴权：import 段加入 `Request, Depends`，并在 `app = FastAPI(...)` 之前新增（`Depends(require_key)` 在模块加载时求值，必须已定义）：
```python
MEETING_BOT_KEY = os.environ.get('MEETING_BOT_KEY', 'dev-key')


async def require_key(request: Request):
    if request.headers.get('X-Meeting-Bot-Key') != MEETING_BOT_KEY:
        raise HTTPException(status_code=401, detail='invalid key')


def _resolve_voice_id(valid_ids: set[str], voice_id: str) -> str:
    return voice_id if voice_id in valid_ids else DEFAULT_VOICE_ID
```

`app = FastAPI(...)` 改为：
```python
app = FastAPI(title='CosyVoice 3 TTS API', version='2.0.0',
              dependencies=[Depends(require_key)])
```

`generate_tts` 里把 `default_vid = _voices[0]['id'] if _voices else ''` / `voice_id = ... else default_vid` 替换为：
```python
    valid_ids = set(VOICE_PROMPTS.keys())
    voice_id = _resolve_voice_id(valid_ids, req.voice_id)
```

`upload_voice` 的 `config_wav_path` 保持 `os.path.join('CosyVoice', 'asset', f'{vid}_norm.wav')` 不变（相对 DATA_DIR 解析）。

- [ ] **Step 6: 运行测试确认通过**

Run: `uv run pytest tests/test_server.py -v`
Expected: `4 passed`（不触发模型加载）。

- [ ] **Step 7: 写 Dockerfile**

`services/cosyvoice/Dockerfile`：
```dockerfile
# CosyVoice3-0.5B TTS 模型服务
# 权重/音色资产不烧入镜像：运行时挂载 D:/AI/AImodles/cosyvoice -> /data
FROM nvidia/cuda:12.6.2-cudnn-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

RUN apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates git ffmpeg libsndfile1 libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && curl -LsSf https://astral.sh/uv/install.sh | sh

WORKDIR /app

COPY pyproject.toml uv.lock .python-version ./
RUN uv sync --frozen --no-dev

COPY server.py ./
COPY voices_config.json ./
COPY third_party/CosyVoice ./CosyVoice

ENV COSYVOICE_DATA=/data \
    COSYVOICE_SRC=/app/CosyVoice \
    TTS_VOICE_ID=zh-male-news \
    MEETING_BOT_KEY=dev-key

EXPOSE 8000
CMD [".venv/bin/python", "-m", "uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8000"]
```

- [ ] **Step 8: 提交**

```powershell
git add services/cosyvoice
git commit -m "feat(cosyvoice): CosyVoice3 容器服务（env 路径/鉴权/默认音色/Dockerfile）"
```

---

### Task 5: insightface 模型服务

**Files:**
- Create: `services/insightface/`（pyproject、uv.lock、.python-version、Dockerfile、.dockerignore、.env.example、app/*、tests/*）
- Move: `services/meeting-bot/app/engines/face.py`、`insightface_engine.py` → `services/insightface/app/engines/`

- [ ] **Step 1: 建目录并迁移引擎**

```powershell
New-Item -ItemType Directory -Force -Path "services\insightface\app\routes", "services\insightface\app\engines", "services\insightface\tests"
Copy-Item "services\meeting-bot\app\engines\face.py" "services\insightface\app\engines\face.py"
Copy-Item "services\meeting-bot\app\engines\insightface_engine.py" "services\insightface\app\engines\insightface_engine.py"
New-Item -ItemType File -Force -Path "services\insightface\app\__init__.py", "services\insightface\app\routes\__init__.py", "services\insightface\app\engines\__init__.py"
```

- [ ] **Step 2: 写 pyproject 并生成 uv.lock**

`services/insightface/pyproject.toml`：
```toml
[project]
name = "insightface"
version = "0.1.0"
description = "InsightFace buffalo_l 人脸模型服务"
requires-python = ">=3.12"
dependencies = [
    "fastapi>=0.115",
    "uvicorn[standard]>=0.30",
    "pydantic-settings>=2.4",
    "python-multipart>=0.0.9",
    "numpy>=1.24",
    "insightface>=0.7.3",
    "onnxruntime>=1.17",
    "opencv-python>=4.9",
]

[dependency-groups]
dev = ["pytest>=8", "httpx>=0.27"]

[tool.pytest.ini_options]
pythonpath = ["."]
testpaths = ["tests"]
```

`services/insightface/.python-version`：`3.12`

`services/insightface/.env.example`：
```dotenv
MEETING_BOT_KEY=dev-key
MODEL_DIR=/app/models
FACE_PROVIDERS=cpu
FACE_RECOGNIZE_THRESHOLD=0.55
```

`services/insightface/.dockerignore`：
```dockerignore
.venv
models
third_party
tests
__pycache__
*.pyc
.pytest_cache
.env
```

Run（工作目录 `services/insightface`）：`uv lock`
Expected: 生成 `uv.lock`。

- [ ] **Step 3: 写失败的路由测试（FACE_ENGINE=mock 避免加载真实模型）**

`services/insightface/tests/test_face_route.py`：
```python
import os

os.environ["FACE_ENGINE"] = "mock"

from fastapi.testclient import TestClient

from app.main import app


class FakeFaceEngine:
    def __init__(self):
        self.enrolled = []

    def recognize(self, image_bytes):
        from app.engines.face import FaceMatch
        return [FaceMatch(worker_id="w1", name="张三", confidence=0.95, bbox=[1, 2, 3, 4])]

    def enroll(self, worker_id, image_bytes, name=""):
        self.enrolled.append((worker_id, name))


def test_recognize():
    with TestClient(app) as client:
        app.state.face_engine = FakeFaceEngine()
        resp = client.post("/recognize", headers={"X-Meeting-Bot-Key": "dev-key"},
                           files={"image": ("m.jpg", b"jpg", "image/jpeg")})
    assert resp.status_code == 200
    assert resp.json()["faces"][0]["workerId"] == "w1"


def test_enroll():
    fake = FakeFaceEngine()
    with TestClient(app) as client:
        app.state.face_engine = fake
        resp = client.post("/enroll", headers={"X-Meeting-Bot-Key": "dev-key"},
                           data={"worker_id": "w2", "name": "李四"},
                           files={"image": ("m.jpg", b"jpg", "image/jpeg")})
    assert resp.status_code == 200
    assert fake.enrolled == [("w2", "李四")]
```

- [ ] **Step 4: 运行测试确认失败**

Run（工作目录 `services/insightface`）：`uv run pytest tests/test_face_route.py -v`
Expected: `ModuleNotFoundError: No module named 'app'`。

- [ ] **Step 5: 写入口/配置/鉴权/路由**

`services/insightface/app/settings.py`：
```python
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    meeting_bot_key: str = "dev-key"
    face_engine: str = "insightface"  # mock | insightface
    model_dir: str = "models"
    face_recognize_threshold: float = 0.55
    face_providers: str = "cpu"  # cpu | gpu


settings = Settings()
```

`services/insightface/app/security.py`（同 sensevoice，import 指向本包 settings）。

`services/insightface/app/main.py`：
```python
from fastapi import Depends, FastAPI

from app.security import require_key
from app.settings import settings
from app.routes.face import router as face_router

app = FastAPI(title="insightface", dependencies=[Depends(require_key)])
app.include_router(face_router)


@app.get("/health")
def health():
    return {"status": "ok"}


@app.on_event("startup")
def startup():
    from app.engines.face import get_face_engine

    app.state.face_engine = get_face_engine(settings.face_engine)
```

`services/insightface/app/routes/face.py`：
```python
from fastapi import APIRouter, File, Form, Request, UploadFile

router = APIRouter()


@router.post("/recognize")
async def recognize(request: Request, image: UploadFile = File(...)):
    data = await image.read()
    faces = request.app.state.face_engine.recognize(data)
    return {"faces": [
        {
            "workerId": f.worker_id,
            "name": f.name,
            "confidence": f.confidence,
            "bbox": f.bbox,
        }
        for f in faces
    ]}


@router.post("/enroll")
async def enroll(
    request: Request,
    worker_id: str = Form(...),
    name: str = Form(""),
    image: UploadFile = File(...),
):
    request.app.state.face_engine.enroll(worker_id, await image.read(), name=name)
    return {"ok": True}
```

`services/insightface/app/engines/face.py` 的工厂里把 import 提示信息从「uv sync --group models」改成「uv sync」。

- [ ] **Step 6: 运行测试确认通过**

Run: `uv run pytest tests/test_face_route.py -v`
Expected: `2 passed`。

- [ ] **Step 7: 写 Dockerfile**

`services/insightface/Dockerfile`：
```dockerfile
# InsightFace buffalo_l 人脸模型服务
# 权重与 faces.json 挂载 D:/AI/AImodles/models -> /app/models
FROM nvidia/cuda:12.6.2-cudnn-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

RUN apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates libgl1 libglib2.0-0 libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && curl -LsSf https://astral.sh/uv/install.sh | sh

WORKDIR /app

COPY pyproject.toml uv.lock .python-version ./
RUN uv sync --frozen --no-dev

COPY app ./app

ENV MODEL_DIR=/app/models \
    FACE_PROVIDERS=cpu \
    FACE_RECOGNIZE_THRESHOLD=0.55 \
    MEETING_BOT_KEY=dev-key

EXPOSE 8103
CMD [".venv/bin/python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8103"]
```

- [ ] **Step 8: 提交**

```powershell
git add services/insightface
git commit -m "feat(insightface): 人脸模型服务容器（迁移引擎+路由）"
```

---

### Task 6: yolo 模型服务

**Files:**
- Create: `services/yolo/`（同 insightface 结构）
- Move: `services/meeting-bot/app/engines/count.py`、`yolo_engine.py` → `services/yolo/app/engines/`

- [ ] **Step 1: 建目录并迁移引擎**

```powershell
New-Item -ItemType Directory -Force -Path "services\yolo\app\routes", "services\yolo\app\engines", "services\yolo\tests"
Copy-Item "services\meeting-bot\app\engines\count.py" "services\yolo\app\engines\count.py"
Copy-Item "services\meeting-bot\app\engines\yolo_engine.py" "services\yolo\app\engines\yolo_engine.py"
New-Item -ItemType File -Force -Path "services\yolo\app\__init__.py", "services\yolo\app\routes\__init__.py", "services\yolo\app\engines\__init__.py"
```

- [ ] **Step 2: 写 pyproject 并生成 uv.lock**

`services/yolo/pyproject.toml`：
```toml
[project]
name = "yolo"
version = "0.1.0"
description = "YOLOv8n 人数统计模型服务"
requires-python = ">=3.12"
dependencies = [
    "fastapi>=0.115",
    "uvicorn[standard]>=0.30",
    "pydantic-settings>=2.4",
    "python-multipart>=0.0.9",
    "numpy>=1.24",
    "ultralytics>=8.2",
    "opencv-python>=4.9",
]

[dependency-groups]
dev = ["pytest>=8", "httpx>=0.27"]

[tool.pytest.ini_options]
pythonpath = ["."]
testpaths = ["tests"]
```

`services/yolo/.python-version`：`3.12`

`services/yolo/.env.example`：
```dotenv
MEETING_BOT_KEY=dev-key
MODEL_DIR=/app/models
COUNT_DEVICE=cpu
```

`services/yolo/.dockerignore`：同 insightface。

Run（工作目录 `services/yolo`）：`uv lock`
Expected: 生成 `uv.lock`。

- [ ] **Step 3: 写失败的路由测试（COUNT_ENGINE=mock 避免加载真实模型）**

`services/yolo/tests/test_count_route.py`：
```python
import os

os.environ["COUNT_ENGINE"] = "mock"

from fastapi.testclient import TestClient

from app.main import app


class FakeCountEngine:
    def count(self, image_bytes):
        return 2


def test_count():
    with TestClient(app) as client:
        app.state.count_engine = FakeCountEngine()
        resp = client.post("/count", headers={"X-Meeting-Bot-Key": "dev-key"},
                           files={"image": ("m.jpg", b"jpg", "image/jpeg")})
    assert resp.status_code == 200
    assert resp.json()["count"] == 2
```

- [ ] **Step 4: 运行测试确认失败**

Run（工作目录 `services/yolo`）：`uv run pytest tests/test_count_route.py -v`
Expected: `ModuleNotFoundError: No module named 'app'`。

- [ ] **Step 5: 写入口/配置/鉴权/路由**

`services/yolo/app/settings.py`：
```python
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    meeting_bot_key: str = "dev-key"
    count_engine: str = "yolo"  # mock | yolo
    model_dir: str = "models"
    count_device: str = "cpu"  # cpu | cuda


settings = Settings()
```

`services/yolo/app/security.py`：同 sensevoice。

`services/yolo/app/main.py`：
```python
from fastapi import Depends, FastAPI

from app.security import require_key
from app.settings import settings
from app.routes.count import router as count_router

app = FastAPI(title="yolo", dependencies=[Depends(require_key)])
app.include_router(count_router)


@app.get("/health")
def health():
    return {"status": "ok"}


@app.on_event("startup")
def startup():
    from app.engines.count import get_count_engine

    app.state.count_engine = get_count_engine(settings.count_engine)
```

`services/yolo/app/routes/count.py`：
```python
from fastapi import APIRouter, File, Request, UploadFile

router = APIRouter()


@router.post("/count")
async def count(request: Request, image: UploadFile = File(...)):
    n = request.app.state.count_engine.count(await image.read())
    return {"count": n}
```

`services/yolo/app/engines/count.py` 的工厂里把 import 提示信息从「uv sync --group models」改成「uv sync」。

- [ ] **Step 6: 运行测试确认通过**

Run: `uv run pytest tests/test_count_route.py -v`
Expected: `1 passed`。

- [ ] **Step 7: 写 Dockerfile**

`services/yolo/Dockerfile`：
```dockerfile
# YOLOv8n 人数统计模型服务
# 权重挂载 D:/AI/AImodles/models -> /app/models
FROM nvidia/cuda:12.6.2-cudnn-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

RUN apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates libgl1 libglib2.0-0 libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && curl -LsSf https://astral.sh/uv/install.sh | sh

WORKDIR /app

COPY pyproject.toml uv.lock .python-version ./
RUN uv sync --frozen --no-dev

COPY app ./app

ENV MODEL_DIR=/app/models \
    COUNT_DEVICE=cpu \
    MEETING_BOT_KEY=dev-key

EXPOSE 8104
CMD [".venv/bin/python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8104"]
```

- [ ] **Step 8: 提交**

```powershell
git add services/yolo
git commit -m "feat(yolo): 人数模型服务容器（迁移引擎+路由）"
```

---

### Task 7: meeting-bot 改造为聚合层

**Files:**
- Modify: `services/meeting-bot/app/settings.py`（重写）
- Modify: `services/meeting-bot/app/routes/asr.py`、`tts.py`、`face.py`、`count.py`（重写为转发）
- Modify: `services/meeting-bot/app/routes/transcribe.py`（重写为调 sensevoice 的后台任务）
- Modify: `services/meeting-bot/pyproject.toml`（去 models 组，加 httpx）
- Modify: `services/meeting-bot/.env.example`
- Modify: `services/meeting-bot/Dockerfile`（瘦身）
- Delete: `services/meeting-bot/app/engines/`（整体）
- Delete: `services/meeting-bot/tests/test_asr.py`、`test_tts.py`、`test_face.py`、`test_count.py`、`test_engines_integration.py`、`test_firered_tts_paths.py`
- Test: `services/meeting-bot/tests/test_proxy.py`、`test_transcribe.py`（重写）

- [ ] **Step 1: 写失败测试（转发路由，mock httpx）**

`services/meeting-bot/tests/test_proxy.py`：
```python
from fastapi.testclient import TestClient

from app.main import app


class FakeResponse:
    status_code = 200

    def __init__(self, payload=None, content=b""):
        self._payload = payload
        self._content = content

    def json(self):
        return self._payload

    @property
    def content(self):
        return self._content

    @property
    def headers(self):
        return {"content-type": "audio/wav"}


class FakeAsyncClient:
    def __init__(self, *args, **kwargs):
        self.calls = []

    async def __aenter__(self):
        return self

    async def __aexit__(self, *exc):
        return False

    async def post(self, url, **kwargs):
        self.calls.append((url, kwargs))
        if "/api/tts" in url:
            return FakeResponse(content=b"RIFF-cosy")
        if "/recognize" in url:
            return FakeResponse(payload={"faces": [{"workerId": "w1"}]})
        if "/enroll" in url:
            return FakeResponse(payload={"ok": True})
        if "/count" in url:
            return FakeResponse(payload={"count": 2})
        return FakeResponse(payload={"text": "转发转写"})


def _client():
    return TestClient(app)


def test_asr_forwards(monkeypatch):
    monkeypatch.setattr("app.routes.asr.httpx.AsyncClient", FakeAsyncClient)
    resp = _client().post("/asr", headers={"X-Meeting-Bot-Key": "dev-key"},
                          files={"audio": ("q.wav", b"data", "audio/wav")})
    assert resp.status_code == 200
    assert resp.json()["text"] == "转发转写"


def test_tts_forwards(monkeypatch):
    monkeypatch.setattr("app.routes.tts.httpx.AsyncClient", FakeAsyncClient)
    resp = _client().post("/tts", headers={"X-Meeting-Bot-Key": "dev-key"},
                          json={"text": "早上好"})
    assert resp.status_code == 200
    assert resp.content == b"RIFF-cosy"


def test_recognize_forwards(monkeypatch):
    monkeypatch.setattr("app.routes.face.httpx.AsyncClient", FakeAsyncClient)
    resp = _client().post("/recognize", headers={"X-Meeting-Bot-Key": "dev-key"},
                          files={"image": ("m.jpg", b"jpg", "image/jpeg")})
    assert resp.status_code == 200
    assert resp.json()["faces"][0]["workerId"] == "w1"


def test_enroll_forwards_fields(monkeypatch):
    fake = FakeAsyncClient()
    monkeypatch.setattr("app.routes.face.httpx.AsyncClient", lambda *a, **kw: fake)
    resp = _client().post("/enroll", headers={"X-Meeting-Bot-Key": "dev-key"},
                          data={"worker_id": "w2", "name": "李四"},
                          files={"image": ("m.jpg", b"jpg", "image/jpeg")})
    assert resp.status_code == 200
    sent = fake.calls[0][1]
    assert sent["data"]["worker_id"] == "w2"
    assert sent["data"]["name"] == "李四"


def test_count_forwards(monkeypatch):
    monkeypatch.setattr("app.routes.count.httpx.AsyncClient", FakeAsyncClient)
    resp = _client().post("/count", headers={"X-Meeting-Bot-Key": "dev-key"},
                          files={"image": ("m.jpg", b"jpg", "image/jpeg")})
    assert resp.status_code == 200
    assert resp.json()["count"] == 2
```

- [ ] **Step 2: 运行测试确认失败**

Run（工作目录 `services/meeting-bot`）：`uv run pytest tests/test_proxy.py -v`
Expected: 失败（现有路由仍是引擎调用，`app.routes.asr.httpx` 不存在）。

- [ ] **Step 3: 重写 settings.py**

`services/meeting-bot/app/settings.py`：
```python
from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    meeting_bot_key: str = "dev-key"
    # 模型服务地址（compose 内走服务名；裸跑开发可改 localhost）
    sensevoice_url: str = "http://sensevoice:8102"
    cosyvoice_url: str = "http://cosyvoice:8000"
    insightface_url: str = "http://insightface:8103"
    yolo_url: str = "http://yolo:8104"
    tts_voice_id: str = "zh-male-news"
    transcribe_max_concurrency: int = 1


settings = Settings()
```

- [ ] **Step 4: 重写四个转发路由**

`services/meeting-bot/app/routes/asr.py`：
```python
import httpx
from fastapi import APIRouter, File, HTTPException, Request, UploadFile

from app.settings import settings

router = APIRouter()


@router.post("/asr")
async def asr(request: Request, audio: UploadFile = File(...)):
    data = await audio.read()
    try:
        async with httpx.AsyncClient(timeout=httpx.Timeout(300.0, connect=10.0)) as client:
            resp = await client.post(
                f"{settings.sensevoice_url}/asr",
                headers={"X-Meeting-Bot-Key": settings.meeting_bot_key},
                files={"audio": (audio.filename or "audio.wav", data, audio.content_type or "audio/wav")},
            )
    except httpx.HTTPError as exc:
        raise HTTPException(status_code=502, detail=f"ASR 服务不可达: {exc}") from exc
    if resp.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"ASR 服务错误: HTTP {resp.status_code}")
    return resp.json()
```

`services/meeting-bot/app/routes/tts.py`：
```python
import httpx
from fastapi import APIRouter, HTTPException
from fastapi.responses import StreamingResponse
from pydantic import BaseModel

from app.settings import settings

router = APIRouter()


class TtsRequest(BaseModel):
    text: str


@router.post("/tts")
async def tts(req: TtsRequest):
    try:
        async with httpx.AsyncClient(timeout=httpx.Timeout(300.0, connect=10.0)) as client:
            resp = await client.post(
                f"{settings.cosyvoice_url}/api/tts",
                headers={"X-Meeting-Bot-Key": settings.meeting_bot_key},
                json={"text": req.text, "voice_id": settings.tts_voice_id, "speed": 1.0},
            )
    except httpx.HTTPError as exc:
        raise HTTPException(status_code=502, detail=f"TTS 服务不可达: {exc}") from exc
    if resp.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"TTS 服务错误: HTTP {resp.status_code}")
    return StreamingResponse(iter([resp.content]), media_type=resp.headers.get("content-type", "audio/wav"))
```

`services/meeting-bot/app/routes/face.py`：
```python
import httpx
from fastapi import APIRouter, File, Form, HTTPException, Request, UploadFile

from app.settings import settings

router = APIRouter()


@router.post("/recognize")
async def recognize(request: Request, image: UploadFile = File(...)):
    data = await image.read()
    try:
        async with httpx.AsyncClient(timeout=httpx.Timeout(120.0, connect=10.0)) as client:
            resp = await client.post(
                f"{settings.insightface_url}/recognize",
                headers={"X-Meeting-Bot-Key": settings.meeting_bot_key},
                files={"image": (image.filename or "image.jpg", data, image.content_type or "image/jpeg")},
            )
    except httpx.HTTPError as exc:
        raise HTTPException(status_code=502, detail=f"人脸服务不可达: {exc}") from exc
    if resp.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"人脸服务错误: HTTP {resp.status_code}")
    return resp.json()


@router.post("/enroll")
async def enroll(
    request: Request,
    worker_id: str = Form(...),
    name: str = Form(""),
    image: UploadFile = File(...),
):
    data = await image.read()
    try:
        async with httpx.AsyncClient(timeout=httpx.Timeout(120.0, connect=10.0)) as client:
            resp = await client.post(
                f"{settings.insightface_url}/enroll",
                headers={"X-Meeting-Bot-Key": settings.meeting_bot_key},
                data={"worker_id": worker_id, "name": name},
                files={"image": (image.filename or "image.jpg", data, image.content_type or "image/jpeg")},
            )
    except httpx.HTTPError as exc:
        raise HTTPException(status_code=502, detail=f"人脸服务不可达: {exc}") from exc
    if resp.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"人脸服务错误: HTTP {resp.status_code}")
    return resp.json()
```

`services/meeting-bot/app/routes/count.py`：
```python
import httpx
from fastapi import APIRouter, File, HTTPException, Request, UploadFile

from app.settings import settings

router = APIRouter()


@router.post("/count")
async def count(request: Request, image: UploadFile = File(...)):
    data = await image.read()
    try:
        async with httpx.AsyncClient(timeout=httpx.Timeout(120.0, connect=10.0)) as client:
            resp = await client.post(
                f"{settings.yolo_url}/count",
                headers={"X-Meeting-Bot-Key": settings.meeting_bot_key},
                files={"image": (image.filename or "image.jpg", data, image.content_type or "image/jpeg")},
            )
    except httpx.HTTPError as exc:
        raise HTTPException(status_code=502, detail=f"人数服务不可达: {exc}") from exc
    if resp.status_code >= 400:
        raise HTTPException(status_code=502, detail=f"人数服务错误: HTTP {resp.status_code}")
    return resp.json()
```

- [ ] **Step 5: 重写 transcribe 路由**

`services/meeting-bot/app/routes/transcribe.py`：
```python
import asyncio
import uuid

import httpx
from fastapi import APIRouter, File, UploadFile

from app.settings import settings

router = APIRouter()
_jobs: dict[str, dict] = {}
_sem = asyncio.Semaphore(settings.transcribe_max_concurrency)


@router.post("/transcribe")
async def start_transcribe(audio: UploadFile = File(...)):
    job_id = uuid.uuid4().hex
    data = await audio.read()
    _jobs[job_id] = {"status": "pending", "text": None}

    async def run():
        _jobs[job_id]["status"] = "running"
        try:
            async with _sem:
                async with httpx.AsyncClient(timeout=httpx.Timeout(600.0, connect=10.0)) as client:
                    resp = await client.post(
                        f"{settings.sensevoice_url}/asr",
                        headers={"X-Meeting-Bot-Key": settings.meeting_bot_key},
                        files={"audio": ("audio.wav", data, "audio/wav")},
                    )
            if resp.status_code >= 400:
                _jobs[job_id] = {"status": "error", "text": f"转写失败: HTTP {resp.status_code}"}
                return
            _jobs[job_id] = {"status": "done", "text": resp.json()["text"]}
        except Exception as exc:
            _jobs[job_id] = {"status": "error", "text": f"转写失败: {exc}"}

    asyncio.create_task(run())
    return {"job_id": job_id}


@router.get("/transcribe/{job_id}")
def get_transcribe(job_id: str):
    job = _jobs.get(job_id)
    if not job:
        return {"status": "not_found"}
    return job
```

- [ ] **Step 6: 写转写任务测试**

`services/meeting-bot/tests/test_transcribe.py`：
```python
import time

from fastapi.testclient import TestClient

from app.main import app


class FakeResponse:
    status_code = 200

    def json(self):
        return {"text": "长音频转写结果"}


class FakeAsyncClient:
    async def __aenter__(self):
        return self

    async def __aexit__(self, *exc):
        return False

    async def post(self, url, **kwargs):
        return FakeResponse()


def test_transcribe_job(monkeypatch):
    monkeypatch.setattr("app.routes.transcribe.httpx.AsyncClient", FakeAsyncClient)
    client = TestClient(app)
    resp = client.post("/transcribe", headers={"X-Meeting-Bot-Key": "dev-key"},
                       files={"audio": ("q.wav", b"data", "audio/wav")})
    job_id = resp.json()["job_id"]
    for _ in range(50):
        r = client.get(f"/transcribe/{job_id}", headers={"X-Meeting-Bot-Key": "dev-key"})
        if r.json()["status"] == "done":
            assert r.json()["text"] == "长音频转写结果"
            return
        time.sleep(0.1)
    raise AssertionError("transcribe job 未完成")
```

- [ ] **Step 7: 运行新测试确认通过**

Run: `uv run pytest tests/test_proxy.py tests/test_transcribe.py tests/test_security.py tests/test_health.py -v`
Expected: 全部通过（`test_health.py`、`test_security.py` 未变，应继续通过）。

- [ ] **Step 8: 删除引擎代码与旧测试**

```powershell
Remove-Item -Recurse -Force "services\meeting-bot\app\engines"
Remove-Item -Force "services\meeting-bot\tests\test_asr.py", "services\meeting-bot\tests\test_tts.py", "services\meeting-bot\tests\test_face.py", "services\meeting-bot\tests\test_count.py", "services\meeting-bot\tests\test_engines_integration.py", "services\meeting-bot\tests\test_firered_tts_paths.py"
```

> 删除前确认 Task 1/5/6 已把 `audio.py`、`face.py`、`insightface_engine.py`、`count.py`、`yolo_engine.py` 复制到新服务（git 历史仍保留全部内容，可随时找回）。

- [ ] **Step 9: 更新 pyproject、.env.example、Dockerfile**

`services/meeting-bot/pyproject.toml`：
```toml
[project]
name = "meeting-bot"
version = "0.2.0"
description = "AI晨会聚合层（转发 ASR/TTS/人脸/人数模型服务）"
requires-python = ">=3.12"
dependencies = [
    "fastapi>=0.115",
    "uvicorn[standard]>=0.30",
    "pydantic-settings>=2.4",
    "python-multipart>=0.0.9",
    "httpx>=0.27",
]

[dependency-groups]
dev = ["pytest>=8", "httpx>=0.27"]

[tool.pytest.ini_options]
pythonpath = ["."]
testpaths = ["tests"]
```

`services/meeting-bot/.env.example`：
```dotenv
MEETING_BOT_KEY=dev-key
SENSEVOICE_URL=http://sensevoice:8102
COSYVOICE_URL=http://cosyvoice:8000
INSIGHTFACE_URL=http://insightface:8103
YOLO_URL=http://yolo:8104
TTS_VOICE_ID=zh-male-news
```

`services/meeting-bot/Dockerfile`（整体替换）：
```dockerfile
# AI 晨会聚合层：只做 HTTP 转发，不装模型
FROM python:3.12-slim

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

RUN apt-get update && apt-get install -y --no-install-recommends curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && curl -LsSf https://astral.sh/uv/install.sh | sh

WORKDIR /app

COPY pyproject.toml uv.lock .python-version ./
RUN uv sync --frozen --no-dev

COPY app ./app

ENV MEETING_BOT_KEY=dev-key

EXPOSE 8101
CMD [".venv/bin/python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8101"]
```

`services/meeting-bot/.python-version`：`3.12`

Run（工作目录 `services/meeting-bot`）：`uv lock`
Expected: `uv.lock` 更新（去掉 models 组）。

- [ ] **Step 10: 全量单测回归**

Run: `uv run pytest -v`
Expected: 除容器冒烟（`test_container_smoke.py` 需 `MEETING_BOT_BASE_URL`，会自动 skip）外全部通过。

- [ ] **Step 11: 提交**

```powershell
git add services/meeting-bot
git commit -m "refactor(meeting-bot): 聚合层化（HTTP 转发 + 长音频任务，移除内嵌引擎）"
```

---

### Task 8: 五服务 compose 编排 + gitignore

**Files:**
- Modify: `services/meeting-bot/docker-compose.yml`（整体替换为五服务）
- Modify: `.gitignore`

- [ ] **Step 1: 写五服务 compose**

`services/meeting-bot/docker-compose.yml`：
```yaml
# AI 晨会模型服务「一模型一容器」编排
# 前置：模型权重位于宿主 D:/AI/AImodles（models/ 与 cosyvoice/）；Docker Desktop 已启用 NVIDIA runtime
services:
  sensevoice:
    build: ../sensevoice
    image: sensevoice:dev
    container_name: sensevoice
    ports:
      - "8102:8102"
    environment:
      MEETING_BOT_KEY: dev-key
      MODEL_DIR: /app/models
      ASR_DEVICE: cpu
    volumes:
      - D:/AI/AImodles/models:/app/models
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://localhost:8102/health"]
      interval: 10s
      timeout: 5s
      retries: 12
      start_period: 30s

  cosyvoice:
    build: ../cosyvoice
    image: cosyvoice:dev
    container_name: cosyvoice
    ports:
      - "8000:8000"
    environment:
      MEETING_BOT_KEY: dev-key
      COSYVOICE_DATA: /data
      TTS_VOICE_ID: zh-male-news
    volumes:
      - D:/AI/AImodles/cosyvoice:/data
    deploy:
      resources:
        reservations:
          devices:
            - driver: nvidia
              count: all
              capabilities: [gpu]
    restart: unless-stopped
    healthcheck:
      test: ["CMD-SHELL", "curl -fsS http://localhost:8000/api/health | grep -q '\"model_loaded\":true'"]
      interval: 10s
      timeout: 5s
      retries: 30
      start_period: 120s

  insightface:
    build: ../insightface
    image: insightface:dev
    container_name: insightface
    ports:
      - "8103:8103"
    environment:
      MEETING_BOT_KEY: dev-key
      MODEL_DIR: /app/models
      FACE_PROVIDERS: cpu
      FACE_RECOGNIZE_THRESHOLD: 0.55
    volumes:
      - D:/AI/AImodles/models:/app/models
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://localhost:8103/health"]
      interval: 10s
      timeout: 5s
      retries: 12
      start_period: 30s

  yolo:
    build: ../yolo
    image: yolo:dev
    container_name: yolo
    ports:
      - "8104:8104"
    environment:
      MEETING_BOT_KEY: dev-key
      MODEL_DIR: /app/models
      COUNT_DEVICE: cpu
    volumes:
      - D:/AI/AImodles/models:/app/models
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://localhost:8104/health"]
      interval: 10s
      timeout: 5s
      retries: 12
      start_period: 30s

  meeting-bot:
    build: .
    image: meeting-bot:dev
    container_name: meeting-bot
    ports:
      - "8101:8101"
    environment:
      MEETING_BOT_KEY: dev-key
      SENSEVOICE_URL: http://sensevoice:8102
      COSYVOICE_URL: http://cosyvoice:8000
      INSIGHTFACE_URL: http://insightface:8103
      YOLO_URL: http://yolo:8104
      TTS_VOICE_ID: zh-male-news
    depends_on:
      sensevoice:
        condition: service_healthy
      cosyvoice:
        condition: service_healthy
      insightface:
        condition: service_healthy
      yolo:
        condition: service_healthy
    restart: unless-stopped
```

- [ ] **Step 2: 校验配置**

Run（仓库根）：`docker compose -f services\meeting-bot\docker-compose.yml config --quiet`
Expected: 无输出、退出码 0。

- [ ] **Step 3: 更新 .gitignore**

在 `.gitignore` 末尾追加：
```gitignore
# 一模型一容器服务（代码入库；权重/venv/第三方源码不入库）
services/sensevoice/models/
services/sensevoice/.venv/
services/sensevoice/third_party/
services/cosyvoice/third_party/
services/cosyvoice/.venv/
services/insightface/models/
services/insightface/.venv/
services/insightface/third_party/
services/yolo/models/
services/yolo/.venv/
services/yolo/third_party/

# 新服务 pytest 用例入库（根 tests/ 规则是测试标书）
!services/sensevoice/tests/
!services/sensevoice/tests/**
!services/cosyvoice/tests/
!services/cosyvoice/tests/**
!services/insightface/tests/
!services/insightface/tests/**
!services/yolo/tests/
!services/yolo/tests/**
services/sensevoice/tests/__pycache__/
services/sensevoice/tests/**/__pycache__/
services/cosyvoice/tests/__pycache__/
services/cosyvoice/tests/**/__pycache__/
services/insightface/tests/__pycache__/
services/insightface/tests/**/__pycache__/
services/yolo/tests/__pycache__/
services/yolo/tests/**/__pycache__/
services/sensevoice/tests/**/*.py[cod]
services/cosyvoice/tests/**/*.py[cod]
services/insightface/tests/**/*.py[cod]
services/yolo/tests/**/*.py[cod]
```

- [ ] **Step 4: 确认测试文件入库**

Run: `git status --short`
Expected: 四个新服务的 `tests/` 出现在未跟踪列表（未被 ignore）。

- [ ] **Step 5: 提交**

```powershell
git add .gitignore services/meeting-bot/docker-compose.yml
git commit -m "feat: 五容器模型服务编排（healthcheck/GPU/数据卷）+ gitignore"
```

---

### Task 9: 部署脚本 deploy-model-services.ps1

**Files:**
- Create: `scripts/deploy-model-services.ps1`

- [ ] **Step 1: 写脚本**

`scripts/deploy-model-services.ps1`：
```powershell
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
        uv run python -c "from funasr import AutoModel; AutoModel(model='iic/SenseVoiceSmall', model_dir=r'$senseDir', disable_update=True, disable_pbar=True)"
        Pop-Location
    } else {
        Write-Host "  已存在，跳过" -ForegroundColor Yellow
    }

    Write-Host "[2/5] CosyVoice3 权重（FunAudioLLM/Fun-CosyVoice3-0.5B-2512）" -ForegroundColor Cyan
    $cosyModel = Join-Path $CosyVoiceRoot "pretrained_models\Fun-CosyVoice3-0.5B"
    if (-not (Test-Path (Join-Path $cosyModel "cosyvoice.yaml"))) {
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
```

- [ ] **Step 2: 提交**

```powershell
git add scripts/deploy-model-services.ps1
git commit -m "feat: deploy-model-services.ps1（停旧进程/权重下载/一键起五容器）"
```

---

### Task 10: 全链路冒烟 + 文档

**Files:**
- Modify: `services/meeting-bot/tests/test_container_smoke.py`（重写）
- Modify: `docs/meeting-bot-deploy.md`

- [ ] **Step 1: 重写容器冒烟测试**

`services/meeting-bot/tests/test_container_smoke.py`：
```python
"""五容器模型服务冒烟：对五个端口 health + 经 meeting-bot 全 API 回归。

运行：
    $env:MEETING_BOT_BASE_URL="http://localhost:8101"
    uv run pytest tests/test_container_smoke.py -v
依赖宿主样例照片 data/meeting-bot/samples/meeting.jpg（缺失时视觉用例跳过）。
"""

import io
import os
import time
import wave

import httpx
import numpy as np
import pytest

BASE = os.environ.get("MEETING_BOT_BASE_URL", "http://localhost:8101").rstrip("/")
KEY = os.environ.get("MEETING_BOT_KEY", "dev-key")
HEADERS = {"X-Meeting-Bot-Key": KEY}

MODEL_SERVICES = {
    "sensevoice": os.environ.get("SENSEVOICE_URL", "http://localhost:8102"),
    "cosyvoice": os.environ.get("COSYVOICE_URL", "http://localhost:8000"),
    "insightface": os.environ.get("INSIGHTFACE_URL", "http://localhost:8103"),
    "yolo": os.environ.get("YOLO_URL", "http://localhost:8104"),
}

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


@pytest.mark.parametrize("name,url", MODEL_SERVICES.items())
def test_model_service_health(name, url):
    r = httpx.get(f"{url}/health", headers=HEADERS, timeout=10)
    assert r.status_code == 200, f"{name} health failed: {r.text}"
    assert r.json()["status"] == "ok"


def test_health():
    r = httpx.get(f"{BASE}/health", headers=HEADERS, timeout=10)
    assert r.status_code == 200
    assert r.json()["status"] == "ok"


def test_asr():
    r = httpx.post(
        f"{BASE}/asr", headers=HEADERS,
        files={"audio": ("t.wav", _tone_wav(), "audio/wav")}, timeout=300,
    )
    assert r.status_code == 200
    assert isinstance(r.json()["text"], str)


def test_tts():
    r = httpx.post(f"{BASE}/tts", headers=HEADERS, json={"text": "今天的安全交底重点有三条。"}, timeout=300)
    assert r.status_code == 200
    assert r.content[:4] == b"RIFF"
    assert len(r.content) > 1024


def test_count():
    img = _sample_image()
    if img is None:
        pytest.skip("缺少样例照片 data/meeting-bot/samples/meeting.jpg")
    r = httpx.post(f"{BASE}/count", headers=HEADERS,
                   files={"image": ("m.jpg", img, "image/jpeg")}, timeout=120)
    assert r.status_code == 200
    assert r.json()["count"] >= 1


def test_recognize():
    img = _sample_image()
    if img is None:
        pytest.skip("缺少样例照片 data/meeting-bot/samples/meeting.jpg")
    r = httpx.post(f"{BASE}/recognize", headers=HEADERS,
                   files={"image": ("m.jpg", img, "image/jpeg")}, timeout=120)
    assert r.status_code == 200
    assert isinstance(r.json()["faces"], list)


def test_transcribe():
    r = httpx.post(f"{BASE}/transcribe", headers=HEADERS,
                   files={"audio": ("t.wav", _tone_wav(1.0), "audio/wav")}, timeout=30)
    assert r.status_code == 200
    job_id = r.json()["job_id"]
    for _ in range(60):
        q = httpx.get(f"{BASE}/transcribe/{job_id}", headers=HEADERS, timeout=30)
        status = q.json()["status"]
        if status == "done":
            assert isinstance(q.json()["text"], str)
            return
        if status == "error":
            raise AssertionError(q.json()["text"])
        time.sleep(1)
    raise AssertionError("transcribe 超时")
```

- [ ] **Step 2: 启动五容器并跑冒烟**

```powershell
docker compose -f services\meeting-bot\docker-compose.yml up -d --build
$env:MEETING_BOT_BASE_URL="http://localhost:8101"
Set-Location services\meeting-bot
uv run pytest tests\test_container_smoke.py -v
```

Expected: 五容器健康 + 7 个用例全部通过（cosyvoice 首加载约 1-2 分钟，sensevoice 首调约 20s）。

- [ ] **Step 3: 更新部署文档**

在 `docs/meeting-bot-deploy.md` 末尾追加：
```markdown
## 九、一模型一容器（2026-08-24）

- 四个模型各一容器：`sensevoice:8102`（SenseVoice-Small）、`cosyvoice:8000`（CosyVoice3-0.5B，默认音色 zh-male-news）、`insightface:8103`（buffalo_l + faces.json）、`yolo:8104`（YOLOv8n）；`meeting-bot:8101` 为聚合层，对外 API 不变。
- 权重位置：`D:\AI\AImodles\models\`（SenseVoiceSmall / buffalo_l / yolov8n.pt / faces.json）、`D:\AI\AImodles\cosyvoice\pretrained_models\Fun-CosyVoice3-0.5B`。
- 启动：`.\scripts\deploy-model-services.ps1`（停旧裸跑 → 下载缺失权重 → `docker compose up -d --build` → 冒烟）。
- 冒烟：`$env:MEETING_BOT_BASE_URL="http://localhost:8101"; uv run pytest services\meeting-bot\tests\test_container_smoke.py -v`。
- 其他应用可直接调模型服务（带 `X-Meeting-Bot-Key`）；DGX 迁移时 `docker buildx --platform linux/arm64` 重建四镜像，compose 网络/挂载/环境变量原样复用。
- FireRed 引擎代码保留在 git 历史（`services/meeting-bot/app/engines/`），回滚 = 旧提交/旧镜像重建；旧权重未删。
```

- [ ] **Step 4: 提交**

```powershell
git add services/meeting-bot/tests/test_container_smoke.py docs/meeting-bot-deploy.md
git commit -m "test/docs: 五容器全链路冒烟 + 部署文档（一模型一容器）"
```

---

### Task 11: 验收与推送

**Files:** 无

- [ ] **Step 1: 验收清单**

```powershell
# 显存：cosyvoice 容器占 GPU，其余 CPU
& 'C:\Windows\System32\nvidia-smi.exe' --query-compute-apps=pid,used_memory --format=csv

# faces.json 持久化（宿主与容器同一文件）
docker exec insightface cat /app/models/faces.json | Select-Object -First 1
Get-Content "D:\AI\AImodles\models\faces.json" | Select-Object -First 1

# 各服务 key 鉴权
curl.exe -s -o NUL -w "%{http_code}" http://localhost:8102/health
```
Expected: 401（无 key）；带 `X-Meeting-Bot-Key: dev-key` 为 200；faces.json 内容一致。

- [ ] **Step 2: 全量单测**

Run（仓库根）：依次执行四个新服务与 meeting-bot 的 `uv run pytest -v`，Expected 全部通过（meeting-bot 容器冒烟需环境变量，已覆盖）。

- [ ] **Step 3: 推送两个远程**

```powershell
git push origin master
git push github master
```

Expected: 两个远程均 `master -> master`。

---

## 风险与回滚

| 风险 | 应对 |
|------|------|
| 8GB 显存：CosyVoice3 GPU + 旧裸跑进程并存会 OOM | 部署脚本第 0 步先停旧进程；cosyvoice 独占 GPU |
| cosyvoice 依赖（onnxruntime/x-transformers 等）在镜像内缺模块 | 以本机已验证 venv（`D:\AI\AImodles\cosyvoice\.venv`）为基准，`uv pip freeze` 对照补全；容器日志有明确 traceback |
| funasr AutoModel 版本差异 | pyproject 锁 `funasr>=1.2,<2`；`uv.lock` 锁定 |
| meeting-bot 转发超时/上游未就绪 | httpx 超时 + 502/503 可读错误；compose depends_on healthcheck |
| 迁移时引擎/测试删错 | 删除前确认新服务已复制（Task 7 Step 8 前置说明）；git 历史可完整找回 |
