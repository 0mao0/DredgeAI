# AI 晨会 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 DredgeAI 平台内交付"AI 晨会"应用：主持人用手机浏览器完成会前录入、晨会稿生成、现场点名、会议录音转写、语音问答和会后报告。

**Architecture:** user-web（Vue 3）新增分步向导应用；DGX 部署 Qwen（vLLM/NIM）+ bge-m3 + services/meeting-bot（FastAPI，托管 FireRedASR/TTS、InsightFace、YOLO）；ABP .NET 8 主服务新增 MeetingBot 模块，把前端与 DGX 服务连接起来，LLM 统一走 ai-gateway，知识检索复用 AnGIneer。

**Tech Stack:** Vue 3 + Vite + ant-design-vue + pinia（前端）；Python 3.11 + FastAPI + uv + pytest（meeting-bot）；ABP .NET 8 + EF Core + PostgreSQL（后端）；vLLM/NIM + bge-m3（DGX 模型）。

---

## 执行优先级（用户指定）

1. Phase 1：把模型部署到 DGX（Qwen、bge-m3、meeting-bot）
2. Phase 2：同步开发 user-web 前端（可 mock 先行）
3. Phase 3：开发 ABP 后端，把 DGX 与前端连接起来

Phase 1 与 Phase 2 相互独立，可并行；Phase 3 依赖 Phase 1 的 meeting-bot 端点与 Phase 2 的 API 模块。

## 现状核查（2026-08-23 更新）

- LLM 已就绪：`DredgeAI/.env` 的 `LLM_CONFIGS` 已含 `Qwen3.6-35B-A3B-FP8`，指向 `https://ai.bim-ace.com/chat/v1`；该端点实测可访问（未带 key 返回 401，说明服务在线），由 ai-gateway 消费
- AnGIneer 已就绪：本机 `D:\AI\AnGIneer` 已部署，`http://localhost:8790/docs` 实测可访问（200）；embedding/reranker 配置齐全
- ai-gateway：当前未运行，`.\start.ps1` 一键启动（连同 PostgreSQL、ABP 后端、双前端）
- 仓库内目前没有任何 ASR/TTS/人脸/人数服务，meeting-bot 是真正需要新建的部分
- 结论：Phase 1 的 Task 1.1（Qwen 部署）、Task 1.2（Embedding）与 AnGIneer 部署**不再需要新建**，执行时改为"验证现有服务连通并记录"；Phase 1 实际新增范围只剩 meeting-bot（Task 1.3–1.11）
- 对应调整：Task 0.1 只需补充 `MEETING_BOT_BASE_URL`、`MEETING_BOT_KEY` 并确认 `ANGINEER_API_KEY`；Task 1.1/1.2 改为验证端点（`/v1/models`、`/v1/embeddings`）后直接标记完成

## Phase 0：前置确认

### Task 0.1：收集服务地址与凭据

**Files:**
- Create: `.env`（仓库根，已 gitignore）
- Create: `.env.example` 增量（不含真实密钥）

- [ ] **Step 1: 收集以下信息**

从用户/运维处确认：DGX 内网 IP、vLLM/NIM 期望端口（默认 8000）、AnGIneer docs-api 地址与 `X-API-Key`、ai-gateway 地址（默认 http://localhost:8200）、meeting-bot 期望端口（默认 8101）。

- [ ] **Step 2: 写入仓库根 .env**

```dotenv
LLM_BASE_URL=http://<dgx-ip>:8000/v1
LLM_MODEL=Qwen3.6-35B-A3B
EMBEDDING_BASE_URL=http://<dgx-ip>:8001/v1
MEETING_BOT_BASE_URL=http://<dgx-ip>:8101
MEETING_BOT_KEY=<生成随机密钥>
ANGINEER_API_KEY=<现有 AnGIneer key>
AI_GATEWAY_BASE_URL=http://localhost:8200
```

- [ ] **Step 3: 确认 .env 未被 git 跟踪**

Run: `git check-ignore .env`
Expected: 输出 `.env`（若未忽略，先加进 `.gitignore`）。

- [ ] **Step 4: Commit**

```bash
git add .env.example .gitignore
git commit -m "chore: AI晨会环境变量示例"
```

### Task 0.2：DGX ARM 兼容性 PoC

**Files:**
- Create: `services/meeting-bot/scripts/arm-poc.py`

- [ ] **Step 1: 在 DGX 上创建 venv 并安装候选依赖**

Run:
```bash
python3 -m venv /opt/meeting-bot/.venv
/opt/meeting-bot/.venv/bin/pip install torch torchaudio onnxruntime insightface numpy
```
Expected: 全部安装成功（个别包若 ARM 无 wheel，记录需源码编译）。

- [ ] **Step 2: 写最小加载脚本 arm-poc.py**

```python
import torch, onnxruntime, insightface
print("torch", torch.__version__)
print("ort", onnxruntime.__version__)
print("insightface", insightface.__version__)
print("cuda", torch.cuda.is_available())
```

- [ ] **Step 3: 在 DGX 运行**

Run: `/opt/meeting-bot/.venv/bin/python scripts/arm-poc.py`
Expected: 打印各版本号与 `cuda True`（或按 DGX 实际 CUDA 状态记录）。

- [ ] **Step 4: 记录结论到 docs/meeting-bot-deploy.md**

记录：哪些包顺利安装、哪些需编译、CUDA 可用性。若 insightface/onnxruntime 失败，记录替代方案（sherpa-onnx 或源码编译）。

- [ ] **Step 5: Commit**

```bash
git add services/meeting-bot/scripts/arm-poc.py docs/meeting-bot-deploy.md
git commit -m "docs: DGX ARM 兼容性 PoC 结论"
```

### Task 0.3：数据准备（设计文档第 14 节）

- [ ] **Step 1: 准备知识库样例文档**

收集 5–10 份真实资料（安全规范 PDF、公司 SOP、项目方案），在 AnGIneer 管理台入库，用其评测功能验证检索精度，记录 Hit@1/3/5 到 `docs/meeting-bot-deploy.md`。

- [ ] **Step 2: 准备人脸测试库**

从花名册导出照片（每人 2–3 张）放到 `data/meeting-bot/workers-sample/`；无真实照片时用 10 张开源人脸图片代替，供 Task 3.5 联调。

- [ ] **Step 3: 准备验收测试集**

编写 20 条典型问答（安全/规范/闲聊混合）与 2 个模拟晨会脚本，存到 `data/meeting-bot/acceptance/`（问答 JSON + 晨会录音/照片），供 Task 3.11 验收回归。

- [ ] **Step 4: Commit**

```bash
git add docs/meeting-bot-deploy.md data/meeting-bot
git commit -m "docs: AI晨会数据准备清单落地"
```

## Phase 1：DGX 模型部署

### Task 1.1：部署 Qwen3.6-35B-A3B（vLLM 或 NIM）

**Files:**
- Create: `scripts/dgx-qwen.md`（部署与验证步骤记录）

- [ ] **Step 1: 检查 NIM 目录是否覆盖该模型**

Run: 在浏览器打开 https://catalog.ngc.nvidia.com 搜索 `Qwen3.6-35B-A3B`（或 DGX Spark 模型集合）。
Expected: 若存在对应 NIM，按 NGC 页面命令拉取运行；若不存在，走 Step 2。

- [ ] **Step 2: 用 vLLM 启动（NIM 不可用时）**

```bash
docker run -d --name qwen-llm --gpus all -p 8000:8000 \
  -v ~/.cache/huggingface:/root/.cache/huggingface \
  vllm/vllm-openai:latest \
  --model Qwen/Qwen3.6-35B-A3B \
  --port 8000 --max-model-len 16384 --gpu-memory-utilization 0.45
```

- [ ] **Step 3: 验证 OpenAI 兼容端点**

```bash
curl http://<dgx-ip>:8000/v1/models
curl http://<dgx-ip>:8000/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"model":"Qwen3.6-35B-A3B","messages":[{"role":"user","content":"用一句话介绍安全交底"}],"stream":true}'
```
Expected: 第一个请求列出模型；第二个返回流式中文文本。

- [ ] **Step 4: 记录启动命令与实测 token/s 到 scripts/dgx-qwen.md**

- [ ] **Step 5: Commit**

```bash
git add scripts/dgx-qwen.md
git commit -m "docs: Qwen DGX 部署与验证记录"
```

### Task 1.2：部署 bge-m3 Embedding

**Files:**
- Create: `scripts/dgx-embedding.md`

- [ ] **Step 1: 用 TEI 容器启动（推荐）**

```bash
docker run -d --name bge-m3 --gpus all -p 8001:80 \
  ghcr.io/huggingface/text-embeddings-inference:latest \
  --model-id BAAI/bge-m3 --dtype float16
```

- [ ] **Step 2: 验证 OpenAI 兼容 embeddings**

```bash
curl http://<dgx-ip>:8001/v1/embeddings \
  -H "Content-Type: application/json" \
  -d '{"model":"BAAI/bge-m3","input":"安全交底"}'
```
Expected: 返回 1024 维向量。

- [ ] **Step 3: 记录到 scripts/dgx-embedding.md 并 Commit**

```bash
git add scripts/dgx-embedding.md
git commit -m "docs: bge-m3 部署与验证记录"
```

### Task 1.3：meeting-bot 脚手架（FastAPI + uv + pytest）

**Files:**
- Create: `services/meeting-bot/pyproject.toml`
- Create: `services/meeting-bot/app/__init__.py`
- Create: `services/meeting-bot/app/settings.py`
- Create: `services/meeting-bot/app/main.py`
- Create: `services/meeting-bot/tests/test_health.py`

- [ ] **Step 1: 写失败测试 test_health.py**

```python
from fastapi.testclient import TestClient
from app.main import app

def test_health():
    client = TestClient(app)
    resp = client.get("/health")
    assert resp.status_code == 200
    assert resp.json() == {"status": "ok"}
```

- [ ] **Step 2: 运行确认失败**

Run: `uv run pytest tests/test_health.py -v`
Expected: FAIL（`ModuleNotFoundError: app.main`）。

- [ ] **Step 3: 写最小实现**

`pyproject.toml`：
```toml
[project]
name = "meeting-bot"
version = "0.1.0"
requires-python = ">=3.11"
dependencies = ["fastapi>=0.115", "uvicorn[standard]>=0.30", "pydantic-settings>=2.4"]

[dependency-groups]
dev = ["pytest>=8", "httpx>=0.27"]
```

`app/settings.py`：
```python
from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    meeting_bot_key: str = "dev-key"
    asr_engine: str = "mock"       # mock | firered
    tts_engine: str = "mock"       # mock | firered
    face_engine: str = "mock"      # mock | insightface
    count_engine: str = "mock"     # mock | yolo
    model_dir: str = "models"

settings = Settings()
```

`app/main.py`：
```python
from fastapi import FastAPI
from app.settings import settings

app = FastAPI(title="meeting-bot")

@app.get("/health")
def health():
    return {"status": "ok"}
```

- [ ] **Step 4: 安装依赖并运行测试**

Run: `cd services/meeting-bot && uv sync && uv run pytest tests/test_health.py -v`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add services/meeting-bot
git commit -m "feat: meeting-bot FastAPI 脚手架与健康检查"
```

### Task 1.4：共享密钥鉴权

**Files:**
- Create: `services/meeting-bot/app/security.py`
- Create: `services/meeting-bot/tests/test_security.py`
- Modify: `services/meeting-bot/app/main.py`

- [ ] **Step 1: 写失败测试**

```python
from fastapi.testclient import TestClient
from app.main import app

def test_health_requires_key():
    client = TestClient(app)
    assert client.get("/health").status_code == 401

def test_health_with_key():
    client = TestClient(app)
    resp = client.get("/health", headers={"X-Meeting-Bot-Key": "dev-key"})
    assert resp.status_code == 200
```

- [ ] **Step 2: 运行确认失败**

Run: `uv run pytest tests/test_security.py -v`
Expected: FAIL（当前 /health 无鉴权）。

- [ ] **Step 3: 实现 security.py 并挂载中间件**

`app/security.py`：
```python
from fastapi import Request, HTTPException
from app.settings import settings

async def require_key(request: Request):
    if request.headers.get("X-Meeting-Bot-Key") != settings.meeting_bot_key:
        raise HTTPException(status_code=401, detail="invalid key")
```

`app/main.py` 挂载：
```python
from fastapi import FastAPI, Depends
from app.security import require_key

app = FastAPI(title="meeting-bot", dependencies=[Depends(require_key)])
```

- [ ] **Step 4: 运行测试**

Run: `uv run pytest tests/test_security.py tests/test_health.py -v`
Expected: 两个用例均 PASS（health 测试需改为带 key 调用）。

- [ ] **Step 5: 修正 test_health.py 中 /health 调用带 key，并 Commit**

```bash
git add services/meeting-bot
git commit -m "feat: meeting-bot 共享密钥鉴权"
```

### Task 1.5：/asr 端点（引擎接口 + mock）

**Files:**
- Create: `services/meeting-bot/app/engines/__init__.py`
- Create: `services/meeting-bot/app/engines/asr.py`
- Create: `services/meeting-bot/app/routes/__init__.py`
- Create: `services/meeting-bot/app/routes/asr.py`
- Create: `services/meeting-bot/tests/test_asr.py`
- Modify: `services/meeting-bot/app/main.py`

- [ ] **Step 1: 写失败测试**

```python
from fastapi.testclient import TestClient
from app.main import app

def test_asr_returns_text():
    client = TestClient(app)
    resp = client.post("/asr", headers={"X-Meeting-Bot-Key": "dev-key"},
                       files={"audio": ("q.wav", b"RIFF-fake-wav", "audio/wav")})
    assert resp.status_code == 200
    assert resp.json()["text"]
```

- [ ] **Step 2: 运行确认失败**

Run: `uv run pytest tests/test_asr.py -v`
Expected: FAIL（404）。

- [ ] **Step 3: 实现引擎接口与 mock**

`app/engines/asr.py`：
```python
from dataclasses import dataclass

@dataclass
class AsrResult:
    text: str

class AsrEngine:
    def transcribe(self, audio_bytes: bytes, sample_rate: int = 16000) -> AsrResult:
        raise NotImplementedError

class MockAsrEngine(AsrEngine):
    def transcribe(self, audio_bytes: bytes, sample_rate: int = 16000) -> AsrResult:
        return AsrResult(text="这是模拟转写文本")

def get_asr_engine(engine_name: str) -> AsrEngine:
    if engine_name == "firered":
        from .firered_asr import FireRedAsrEngine
        return FireRedAsrEngine()
    return MockAsrEngine()
```

`app/routes/asr.py`：
```python
from fastapi import APIRouter, UploadFile, File
from app.settings import settings
from app.engines.asr import get_asr_engine

router = APIRouter()
_engine = get_asr_engine(settings.asr_engine)

@router.post("/asr")
async def asr(audio: UploadFile = File(...)):
    data = await audio.read()
    result = _engine.transcribe(data)
    return {"text": result.text}
```

`app/main.py` 注册路由：
```python
from fastapi import FastAPI, Depends
from app.security import require_key
from app.routes.asr import router as asr_router

app = FastAPI(title="meeting-bot", dependencies=[Depends(require_key)])
app.include_router(asr_router)
```

- [ ] **Step 4: 运行测试**

Run: `uv run pytest tests/test_asr.py -v`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add services/meeting-bot
git commit -m "feat: /asr 端点与引擎接口（mock 实现）"
```

### Task 1.6：/tts 端点（流式 + mock）

**Files:**
- Create: `services/meeting-bot/app/engines/tts.py`
- Create: `services/meeting-bot/app/routes/tts.py`
- Create: `services/meeting-bot/tests/test_tts.py`
- Modify: `services/meeting-bot/app/main.py`

- [ ] **Step 1: 写失败测试**

```python
from fastapi.testclient import TestClient
from app.main import app

def test_tts_returns_audio():
    client = TestClient(app)
    resp = client.post("/tts", headers={"X-Meeting-Bot-Key": "dev-key"},
                       json={"text": "早上好"})
    assert resp.status_code == 200
    assert resp.headers["content-type"].startswith("audio/")
    assert resp.content
```

- [ ] **Step 2: 运行确认失败**

Run: `uv run pytest tests/test_tts.py -v`
Expected: FAIL（404）。

- [ ] **Step 3: 实现 TTS 引擎与路由**

`app/engines/tts.py`：
```python
class TtsEngine:
    def synthesize(self, text: str) -> bytes:
        raise NotImplementedError

class MockTtsEngine(TtsEngine):
    def synthesize(self, text: str) -> bytes:
        return b"RIFF-fake-wav-" + text.encode("utf-8")

def get_tts_engine(engine_name: str) -> TtsEngine:
    if engine_name == "firered":
        from .firered_tts import FireRedTtsEngine
        return FireRedTtsEngine()
    return MockTtsEngine()
```

`app/routes/tts.py`：
```python
from fastapi import APIRouter
from fastapi.responses import StreamingResponse
from pydantic import BaseModel
from app.settings import settings
from app.engines.tts import get_tts_engine

router = APIRouter()
_engine = get_tts_engine(settings.tts_engine)

class TtsRequest(BaseModel):
    text: str

@router.post("/tts")
def tts(req: TtsRequest):
    audio = _engine.synthesize(req.text)
    return StreamingResponse(iter([audio]), media_type="audio/wav")
```

- [ ] **Step 4: 注册路由并运行测试**

`app/main.py` 增加 `app.include_router(tts_router)`。
Run: `uv run pytest tests/test_tts.py -v`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add services/meeting-bot
git commit -m "feat: /tts 端点（流式 + mock 实现）"
```

### Task 1.7：/recognize 与 /enroll 端点

**Files:**
- Create: `services/meeting-bot/app/engines/face.py`
- Create: `services/meeting-bot/app/routes/face.py`
- Create: `services/meeting-bot/tests/test_face.py`
- Modify: `services/meeting-bot/app/main.py`

- [ ] **Step 1: 写失败测试**

```python
from fastapi.testclient import TestClient
from app.main import app

def test_recognize_returns_faces():
    client = TestClient(app)
    resp = client.post("/recognize", headers={"X-Meeting-Bot-Key": "dev-key"},
                       files={"image": ("g.jpg", b"fake-jpeg", "image/jpeg")})
    assert resp.status_code == 200
    assert "faces" in resp.json()
```

- [ ] **Step 2: 运行确认失败**

Run: `uv run pytest tests/test_face.py -v`
Expected: FAIL（404）。

- [ ] **Step 3: 实现 face 引擎与路由**

`app/engines/face.py`：
```python
from dataclasses import dataclass, field

@dataclass
class FaceMatch:
    worker_id: str | None
    name: str | None
    confidence: float
    bbox: list[float] = field(default_factory=list)

class FaceEngine:
    def recognize(self, image_bytes: bytes) -> list[FaceMatch]:
        raise NotImplementedError
    def enroll(self, worker_id: str, image_bytes: bytes) -> None:
        raise NotImplementedError

class MockFaceEngine(FaceEngine):
    def recognize(self, image_bytes: bytes) -> list[FaceMatch]:
        return []
    def enroll(self, worker_id: str, image_bytes: bytes) -> None:
        return None

def get_face_engine(engine_name: str) -> FaceEngine:
    if engine_name == "insightface":
        from .insightface_engine import InsightFaceEngine
        return InsightFaceEngine()
    return MockFaceEngine()
```

`app/routes/face.py`：
```python
from fastapi import APIRouter, UploadFile, File
from pydantic import BaseModel
from app.settings import settings
from app.engines.face import get_face_engine

router = APIRouter()
_engine = get_face_engine(settings.face_engine)

@router.post("/recognize")
async def recognize(image: UploadFile = File(...)):
    data = await image.read()
    faces = _engine.recognize(data)
    return {"faces": [f.__dict__ for f in faces]}

class EnrollRequest(BaseModel):
    worker_id: str

@router.post("/enroll")
async def enroll(worker_id: str = ..., image: UploadFile = File(...)):
    _engine.enroll(worker_id, await image.read())
    return {"ok": True}
```

- [ ] **Step 4: 注册路由并运行测试**

`app/main.py` 增加 `app.include_router(face_router)`。
Run: `uv run pytest tests/test_face.py -v`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add services/meeting-bot
git commit -m "feat: /recognize 与 /enroll 端点（mock 实现）"
```

### Task 1.8：/count 端点

**Files:**
- Create: `services/meeting-bot/app/engines/count.py`
- Create: `services/meeting-bot/app/routes/count.py`
- Create: `services/meeting-bot/tests/test_count.py`
- Modify: `services/meeting-bot/app/main.py`

- [ ] **Step 1: 写失败测试**

```python
from fastapi.testclient import TestClient
from app.main import app

def test_count_returns_number():
    client = TestClient(app)
    resp = client.post("/count", headers={"X-Meeting-Bot-Key": "dev-key"},
                       files={"image": ("g.jpg", b"fake-jpeg", "image/jpeg")})
    assert resp.status_code == 200
    assert "count" in resp.json()
```

- [ ] **Step 2: 运行确认失败**

Run: `uv run pytest tests/test_count.py -v`
Expected: FAIL（404）。

- [ ] **Step 3: 实现 count 引擎与路由**

`app/engines/count.py`：
```python
class CountEngine:
    def count(self, image_bytes: bytes) -> int:
        raise NotImplementedError

class MockCountEngine(CountEngine):
    def count(self, image_bytes: bytes) -> int:
        return 0

def get_count_engine(engine_name: str) -> CountEngine:
    if engine_name == "yolo":
        from .yolo_engine import YoloCountEngine
        return YoloCountEngine()
    return MockCountEngine()
```

`app/routes/count.py`：
```python
from fastapi import APIRouter, UploadFile, File
from app.settings import settings
from app.engines.count import get_count_engine

router = APIRouter()
_engine = get_count_engine(settings.count_engine)

@router.post("/count")
async def count(image: UploadFile = File(...)):
    n = _engine.count(await image.read())
    return {"count": n}
```

- [ ] **Step 4: 注册路由并运行测试**

`app/main.py` 增加 `app.include_router(count_router)`。
Run: `uv run pytest tests/test_count.py -v`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add services/meeting-bot
git commit -m "feat: /count 端点（mock 实现）"
```

### Task 1.9：/transcribe 异步转写（job 模式）

**Files:**
- Create: `services/meeting-bot/app/routes/transcribe.py`
- Create: `services/meeting-bot/tests/test_transcribe.py`
- Modify: `services/meeting-bot/app/main.py`

- [ ] **Step 1: 写失败测试**

```python
from fastapi.testclient import TestClient
from app.main import app

def test_transcribe_job_flow():
    client = TestClient(app)
    resp = client.post("/transcribe", headers={"X-Meeting-Bot-Key": "dev-key"},
                       files={"audio": ("m.wav", b"RIFF-fake-wav", "audio/wav")})
    assert resp.status_code == 200
    job_id = resp.json()["job_id"]
    status = client.get(f"/transcribe/{job_id}", headers={"X-Meeting-Bot-Key": "dev-key"})
    assert status.status_code == 200
    assert "status" in status.json()
```

- [ ] **Step 2: 运行确认失败**

Run: `uv run pytest tests/test_transcribe.py -v`
Expected: FAIL（404）。

- [ ] **Step 3: 实现 job 模式路由（内存任务表，v1 够用）**

`app/routes/transcribe.py`：
```python
import asyncio, uuid
from fastapi import APIRouter, UploadFile, File
from app.settings import settings
from app.engines.asr import get_asr_engine

router = APIRouter()
_engine = get_asr_engine("firered" if settings.asr_engine == "firered" else "mock")
_jobs: dict[str, dict] = {}

@router.post("/transcribe")
async def start_transcribe(audio: UploadFile = File(...)):
    job_id = uuid.uuid4().hex
    data = await audio.read()
    _jobs[job_id] = {"status": "pending", "text": None}
    async def run():
        _jobs[job_id]["status"] = "running"
        await asyncio.sleep(0.01)  # v1 mock；真实实现换成 FireRedASR-LLM 调用
        _jobs[job_id]["text"] = _engine.transcribe(data).text
        _jobs[job_id]["status"] = "done"
    asyncio.create_task(run())
    return {"job_id": job_id}

@router.get("/transcribe/{job_id}")
def get_transcribe(job_id: str):
    job = _jobs.get(job_id)
    if not job:
        return {"status": "not_found"}
    return job
```

- [ ] **Step 4: 注册路由并运行测试**

`app/main.py` 增加 `app.include_router(transcribe_router)`。
Run: `uv run pytest tests/test_transcribe.py -v`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add services/meeting-bot
git commit -m "feat: /transcribe 异步 job 端点（mock 实现）"
```

### Task 1.10：真实模型接入（FireRedASR/TTS、InsightFace、YOLO）

**Files:**
- Create: `services/meeting-bot/app/engines/firered_asr.py`
- Create: `services/meeting-bot/app/engines/firered_tts.py`
- Create: `services/meeting-bot/app/engines/insightface_engine.py`
- Create: `services/meeting-bot/app/engines/yolo_engine.py`
- Create: `services/meeting-bot/tests/test_engines_integration.py`（标记为集成测试）
- Create: `services/meeting-bot/.env.example`

- [ ] **Step 1: 按官方 README 跑通 FireRedASR 与 FireRedTTS**

克隆 `https://github.com/FireRedTeam/FireRedASR` 与 `https://github.com/FireRedTeam/FireRedTTS`，按其 README 在 DGX 上跑通官方推理示例（音频转写、文本合成），记录依赖与模型路径。

- [ ] **Step 2: 封装 FireRedASR 到 firered_asr.py**

在 `FireRedAsrEngine.transcribe()` 内复用官方示例的推理调用，保持 `AsrEngine` 接口签名不变，模型路径从 `settings.model_dir` 读取。启动时若模型缺失则抛清晰错误。

- [ ] **Step 3: 封装 FireRedTTS 到 firered_tts.py**

`FireRedTtsEngine.synthesize()` 返回 WAV 字节，复用官方示例推理调用；合成前做文本归一化（数字/英文转中文读法，可先用官方 G2P）。

- [ ] **Step 4: 封装 InsightFace 到 insightface_engine.py**

```python
import numpy as np
from app.engines.face import FaceEngine, FaceMatch

class InsightFaceEngine(FaceEngine):
    def __init__(self, model_dir: str = "models"):
        import insightface
        from insightface.app import FaceAnalysis
        self.app = FaceAnalysis(name="buffalo_l", root=model_dir)
        self.app.prepare(ctx_id=0)

    def recognize(self, image_bytes: bytes) -> list[FaceMatch]:
        import cv2
        img = cv2.imdecode(np.frombuffer(image_bytes, np.uint8), cv2.IMREAD_COLOR)
        dets = self.app.get(img)
        return [FaceMatch(worker_id=None, name=None, confidence=float(d.det_score), bbox=list(d.bbox))
                for d in dets]
```

- [ ] **Step 5: 封装 YOLO 到 yolo_engine.py**

用 `ultralytics` 加载 `yolov8n.pt`，`model(image_bytes, verbose=False)` 统计 `cls==0`（person）的框数。

- [ ] **Step 6: 写集成测试并运行**

`tests/test_engines_integration.py` 用真实样例音频/图片断言：ASR 返回非空文本、TTS 返回 WAV 头、recognize 在含人照片上返回至少 1 个 FaceMatch、count 返回 >= 1。设置 `asr_engine=firered` 等环境变量后运行：
Run: `uv run pytest tests/test_engines_integration.py -v`
Expected: 全 PASS（在 DGX 上）。

- [ ] **Step 7: 提供 .env.example 并 Commit**

```bash
git add services/meeting-bot
git commit -m "feat: meeting-bot 真实模型引擎接入"
```

### Task 1.11：部署脚本与全链路冒烟

**Files:**
- Create: `scripts/deploy-meeting-bot.ps1`

- [ ] **Step 1: 写部署脚本**

```powershell
$root = Split-Path -Parent $PSScriptRoot
Set-Location "$root\services\meeting-bot"
uv sync
uv run uvicorn app.main:app --host 0.0.0.0 --port 8101
```

- [ ] **Step 2: 启动并冒烟全部端点**

Run: `.\scripts\deploy-meeting-bot.ps1`
Run: `curl http://<dgx-ip>:8101/health -H "X-Meeting-Bot-Key: $env:MEETING_BOT_KEY"`
Expected: `{"status":"ok"}`；随后依次冒烟 /asr、/tts、/recognize、/count、/transcribe。

- [ ] **Step 3: Commit**

```bash
git add scripts/deploy-meeting-bot.ps1
git commit -m "feat: meeting-bot 部署脚本与冒烟清单"
```

## Phase 2：user-web 前端（与 Phase 1 并行）

按 AGENTS.md「新模块开发清单」顺序执行，每个任务完成后运行 `pnpm run typecheck`。

### Task 2.1：AI 晨会类型定义

**Files:**
- Create: `packages/shared/src/core/types/aiMeeting.ts`
- Modify: `packages/shared/src/core/types/index.ts`（追加导出）
- Modify: `user-web/src/types.ts`（若为 barrel，追加导出）

- [ ] **Step 1: 创建类型文件**

```ts
export type MeetingStatus = 'draft' | 'prepared' | 'rollcall' | 'ongoing' | 'completed'
export type AttendanceStatus = 'present' | 'absent' | 'late' | 'unrecognized'

export interface PreInfo {
  date: string
  weather: string
  tasks: string
  riskPoints: string
}

export interface SpeechDraftDto {
  id: string
  content: string
  status: 'draft' | 'generated' | 'confirmed'
  updatedAt: string
}

export interface AttendanceItemDto {
  workerId: string
  name: string
  team: string
  status: AttendanceStatus
  confidence: number
}

export interface QaRecordDto {
  id: string
  question: string
  answer: string
  intentType: 'knowledge' | 'chitchat' | 'meeting'
  sources: string[]
  createdAt: string
}

export interface ReportDto {
  id: string
  transcript: string
  attendance: AttendanceItemDto[]
  qaRecords: QaRecordDto[]
  createdAt: string
}

export interface WorkerDto {
  id: string
  name: string
  employeeNo: string
  team: string
  faceStatus: 'enrolled' | 'pending'
}

export interface MeetingRecordDto {
  id: string
  date: string
  preInfo: PreInfo
  status: MeetingStatus
  speechDraft?: SpeechDraftDto
  attendance: AttendanceItemDto[]
  qaRecords: QaRecordDto[]
  report?: ReportDto
  createdAt: string
}
```

- [ ] **Step 2: 在两个 barrel 文件追加导出**

在 `packages/shared/src/core/types/index.ts` 与 `user-web/src/types.ts` 中追加 `export * from './aiMeeting'`（或按现有 barrel 风格逐项导出）。

- [ ] **Step 3: typecheck 并 Commit**

Run: `pnpm run typecheck`
Expected: 无类型错误。
```bash
git add packages/shared/src/core/types/aiMeeting.ts packages/shared/src/core/types/index.ts user-web/src/types.ts
git commit -m "feat: AI晨会共享类型定义"
```

### Task 2.2：URL 契约声明

**Files:**
- Modify: `packages/shared/src/core/api/urls.ts`

- [ ] **Step 1: 在 urls.ts 追加 AI 晨会段**

```ts
  // AI 晨会
  meetingRecord: '/meeting/records',
  meetingSpeechGenerate: '/meeting/records/:id/speech/generate',
  meetingSpeechDraft: '/meeting/records/:id/speech',
  meetingStart: '/meeting/records/:id/start',
  meetingAttendanceRecognize: '/meeting/records/:id/attendance/recognize',
  meetingAttendance: '/meeting/records/:id/attendance',
  meetingQa: '/meeting/records/:id/qa',
  meetingQaAudio: '/meeting/records/:id/qa/audio',
  meetingRecording: '/meeting/records/:id/recording',
  meetingComplete: '/meeting/records/:id/complete',
  meetingReport: '/meeting/records/:id/report',
  meetingWorkers: '/meeting/workers',
```

- [ ] **Step 2: typecheck 并 Commit**

```bash
git add packages/shared/src/core/api/urls.ts
git commit -m "feat: AI晨会 URL 契约"
```

### Task 2.3：mock 数据与 mock 路由

**Files:**
- Create: `packages/shared/src/mock/data/aiMeeting.ts`
- Create: `user-web/src/mock/routes/aiMeeting.ts`
- Modify: `user-web/src/mock/index.ts`
- Modify: `user-web/src/utils/constants.ts`

- [ ] **Step 1: 创建共享 mock 数据（内存态）**

```ts
import type { MeetingRecordDto, SpeechDraftDto, WorkerDto } from '@shared/types'

export const mockWorkers: WorkerDto[] = [
  { id: 'w-001', name: '张建国', employeeNo: 'A001', team: '钢筋班', faceStatus: 'enrolled' },
  { id: 'w-002', name: '李大海', employeeNo: 'A002', team: '模板班', faceStatus: 'enrolled' },
  { id: 'w-003', name: '王强', employeeNo: 'A003', team: '电工班', faceStatus: 'pending' },
]

export const mockMeetings: MeetingRecordDto[] = []
let nextMeetingId = 1

export function createMockMeeting(preInfo: MeetingRecordDto['preInfo']): MeetingRecordDto {
  const id = `meeting-${nextMeetingId++}`
  const meeting: MeetingRecordDto = {
    id,
    date: preInfo.date,
    preInfo,
    status: 'draft',
    attendance: [],
    qaRecords: [],
    createdAt: new Date().toISOString(),
  }
  mockMeetings.push(meeting)
  return meeting
}

export function generateMockSpeech(id: string): SpeechDraftDto {
  const meeting = mockMeetings.find((m) => m.id === id)
  const p = meeting?.preInfo
  const draft: SpeechDraftDto = {
    id: `speech-${id}`,
    content:
      `各位工友早上好！今天是${p?.date ?? ''}，天气${p?.weather ?? '晴'}。\n` +
      `今日任务：${p?.tasks ?? '按计划施工'}。\n` +
      `风险提示：${p?.riskPoints ?? '注意安全'}。\n` +
      `请各班组长核对人员，戴好安全帽，开始今天的工作。`,
    status: 'generated',
    updatedAt: new Date().toISOString(),
  }
  if (meeting) meeting.speechDraft = draft
  return draft
}
```

- [ ] **Step 2: 创建 mock 路由注册器**

`user-web/src/mock/routes/aiMeeting.ts`：
```ts
import type MockAdapter from 'axios-mock-adapter'
import { mockWorkers, mockMeetings, createMockMeeting, generateMockSpeech } from '@shared/mock/data/aiMeeting'
import type { QaRecordDto } from '@shared/types'

export function registerMeetingMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  const meetingId = (url: string | undefined): string =>
    url?.match(/\/api\/meeting\/records\/([^/]+)\//)?.[1] ?? ''

  mock.onPost('/api/meeting/records').reply((config) => [200, createMockMeeting(JSON.parse(config.data))])
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/speech\/generate$/).reply((config) => {
    return [200, generateMockSpeech(meetingId(config.url))]
  })
  mock.onGet(/\/api\/meeting\/records\/[^/]+\/speech$/).reply((config) => {
    return [200, mockMeetings.find((m) => m.id === meetingId(config.url))?.speechDraft ?? null]
  })
  mock.onPut(/\/api\/meeting\/records\/[^/]+\/speech$/).reply((config) => {
    const meeting = mockMeetings.find((m) => m.id === meetingId(config.url))
    if (meeting?.speechDraft) {
      meeting.speechDraft.content = JSON.parse(config.data).content
      meeting.speechDraft.status = 'confirmed'
    }
    return [200, meeting?.speechDraft ?? null]
  })
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/start$/).reply((config) => {
    const meeting = mockMeetings.find((m) => m.id === meetingId(config.url))
    if (meeting) meeting.status = 'rollcall'
    return [200, meeting]
  })
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/attendance\/recognize$/).reply((config) => {
    const meeting = mockMeetings.find((m) => m.id === meetingId(config.url))
    if (meeting) {
      meeting.attendance = [
        { workerId: 'w-001', name: '张建国', team: '钢筋班', status: 'present', confidence: 0.96 },
        { workerId: 'w-002', name: '李大海', team: '模板班', status: 'present', confidence: 0.92 },
      ]
    }
    return [200, { faces: meeting?.attendance ?? [] }]
  })
  mock.onGet(/\/api\/meeting\/records\/[^/]+\/attendance$/).reply((config) => {
    return [200, mockMeetings.find((m) => m.id === meetingId(config.url))?.attendance ?? []]
  })
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/qa$/).reply((config) => {
    const id = meetingId(config.url)
    const q = JSON.parse(config.data).question
    const rec: QaRecordDto = {
      id: `qa-${Date.now()}`,
      question: q,
      answer: '根据知识库检索，请正确佩戴安全帽并遵守现场安全规程。',
      intentType: 'knowledge',
      sources: ['mock-source'],
      createdAt: new Date().toISOString(),
    }
    mockMeetings.find((m) => m.id === id)?.qaRecords.push(rec)
    return [200, rec]
  })
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/qa\/audio$/).reply((config) => {
    const id = meetingId(config.url)
    const rec: QaRecordDto = {
      id: `qa-${Date.now()}`,
      question: '（语音）今天需要注意什么？',
      answer: '今日重点注意高处作业与临边防护。',
      intentType: 'meeting',
      sources: [],
      createdAt: new Date().toISOString(),
    }
    mockMeetings.find((m) => m.id === id)?.qaRecords.push(rec)
    return [200, rec]
  })
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/recording$/).reply((config) => {
    return [200, mockMeetings.find((m) => m.id === meetingId(config.url)) ?? null]
  })
  mock.onPost(/\/api\/meeting\/records\/[^/]+\/complete$/).reply((config) => {
    const id = meetingId(config.url)
    const meeting = mockMeetings.find((m) => m.id === id)
    if (meeting) {
      meeting.status = 'completed'
      meeting.report = {
        id: `report-${id}`,
        transcript: '（转写稿）各位工友早上好……',
        attendance: meeting.attendance,
        qaRecords: meeting.qaRecords,
        createdAt: new Date().toISOString(),
      }
    }
    return [200, meeting]
  })
  mock.onGet(/\/api\/meeting\/records\/[^/]+\/report$/).reply((config) => {
    return [200, mockMeetings.find((m) => m.id === meetingId(config.url))?.report ?? null]
  })
  mock.onGet('/api/meeting/workers').reply(wrap(() => mockWorkers))
}
```

- [ ] **Step 3: 注册 mock**

`user-web/src/utils/constants.ts` 的 `MOCK_MODULES` 增加 `meeting: true`。
`user-web/src/mock/index.ts` 增加 import 与注册：
```ts
import { registerMeetingMock } from './routes/aiMeeting'
// 在 registerMock() 内、其他注册器旁：
if (MOCK_MODULES.meeting) registerMeetingMock(mock, wrap)
```

- [ ] **Step 4: typecheck 并 Commit**

```bash
git add packages/shared/src/mock/data/aiMeeting.ts user-web/src/mock user-web/src/utils/constants.ts
git commit -m "feat: AI晨会 mock 数据与路由"
```

### Task 2.4：API 模块

**Files:**
- Create: `user-web/src/api/modules/aiMeeting.ts`

- [ ] **Step 1: 创建 API 模块（全部经 request 封装，禁止组件直连）**

```ts
import request from '@/api/request'
import { urls, fillUrl } from '@shared/core/api'
import type {
  MeetingRecordDto,
  SpeechDraftDto,
  AttendanceItemDto,
  QaRecordDto,
  ReportDto,
  WorkerDto,
  PreInfo,
} from '@/types'

export function createMeeting(preInfo: PreInfo): Promise<MeetingRecordDto> {
  return request.post<MeetingRecordDto>(urls.meetingRecord, preInfo)
}

export function generateSpeech(id: string): Promise<SpeechDraftDto> {
  return request.post<SpeechDraftDto>(fillUrl(urls.meetingSpeechGenerate, { id }))
}

export function getSpeechDraft(id: string): Promise<SpeechDraftDto | null> {
  return request.get<SpeechDraftDto | null>(fillUrl(urls.meetingSpeechDraft, { id }))
}

export function saveSpeechDraft(id: string, content: string): Promise<SpeechDraftDto> {
  return request.put<SpeechDraftDto>(fillUrl(urls.meetingSpeechDraft, { id }), { content })
}

export function startMeeting(id: string): Promise<MeetingRecordDto> {
  return request.post<MeetingRecordDto>(fillUrl(urls.meetingStart, { id }))
}

export function recognizeAttendance(id: string, photo: Blob): Promise<{ faces: AttendanceItemDto[] }> {
  const form = new FormData()
  form.append('image', photo, 'attendance.jpg')
  return request.post<{ faces: AttendanceItemDto[] }>(fillUrl(urls.meetingAttendanceRecognize, { id }), form)
}

export function getAttendance(id: string): Promise<AttendanceItemDto[]> {
  return request.get<AttendanceItemDto[]>(fillUrl(urls.meetingAttendance, { id }))
}

export function askQa(id: string, question: string): Promise<QaRecordDto> {
  return request.post<QaRecordDto>(fillUrl(urls.meetingQa, { id }), { question })
}

export function askQaAudio(id: string, audio: Blob): Promise<QaRecordDto> {
  const form = new FormData()
  form.append('audio', audio, 'question.webm')
  return request.post<QaRecordDto>(fillUrl(urls.meetingQaAudio, { id }), form)
}

export function uploadMeetingRecording(id: string, audio: Blob): Promise<MeetingRecordDto> {
  const form = new FormData()
  form.append('audio', audio, 'meeting.webm')
  return request.post<MeetingRecordDto>(fillUrl(urls.meetingRecording, { id }), form)
}

export function completeMeeting(id: string): Promise<MeetingRecordDto> {
  return request.post<MeetingRecordDto>(fillUrl(urls.meetingComplete, { id }))
}

export function getReport(id: string): Promise<ReportDto | null> {
  return request.get<ReportDto | null>(fillUrl(urls.meetingReport, { id }))
}

export function getWorkers(): Promise<WorkerDto[]> {
  return request.get<WorkerDto[]>(urls.meetingWorkers)
}
```

- [ ] **Step 2: typecheck 并 Commit**

```bash
git add user-web/src/api/modules/aiMeeting.ts
git commit -m "feat: AI晨会 API 模块"
```

### Task 2.5：路由 manifest 注册

**Files:**
- Modify: `user-web/src/router/manifests.ts`

- [ ] **Step 1: 注册 ai-meeting 应用入口**

```ts
  {
    id: 'ai-meeting',
    route: '/ai-meeting',
    name: 'AiMeeting',
    title: 'AI晨会',
    icon: 'TeamOutlined',
    component: () => import('@/views/ai-meeting/index.vue'),
    defaultVisible: true,
    category: '施工',
  },
```

- [ ] **Step 2: typecheck 并 Commit**

```bash
git add user-web/src/router/manifests.ts
git commit -m "feat: AI晨会路由注册"
```

### Task 2.6：分步向导骨架

**Files:**
- Create: `user-web/src/views/ai-meeting/index.vue`

- [ ] **Step 1: 创建向导骨架（index.vue 唯一持有业务状态）**

```vue
<script setup lang="ts">
import { ref } from 'vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import type { MeetingRecordDto, PreInfo } from '@/types'
import {
  createMeeting,
  generateSpeech,
  startMeeting,
  completeMeeting,
} from '@/api/modules/aiMeeting'
import MeetingInfoStep from './components/MeetingInfoStep.vue'
import SpeechDraftStep from './components/SpeechDraftStep.vue'
import AttendanceStep from './components/AttendanceStep.vue'
import MeetingStep from './components/MeetingStep.vue'
import ReportStep from './components/ReportStep.vue'

const current = ref(0)
const meeting = ref<MeetingRecordDto | null>(null)
const loading = ref(false)

async function handleCreate(preInfo: PreInfo): Promise<void> {
  loading.value = true
  try {
    meeting.value = await createMeeting(preInfo)
    current.value = 1
  } finally {
    loading.value = false
  }
}

async function handleSpeechGenerated(): Promise<void> {
  if (!meeting.value) return
  meeting.value = await startMeeting(meeting.value.id)
  current.value = 2
}

async function handleMeetingDone(): Promise<void> {
  if (!meeting.value) return
  loading.value = true
  try {
    meeting.value = await completeMeeting(meeting.value.id)
    current.value = 4
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="ai-meeting-page">
    <PageHeader title="AI晨会" description="会前录入 → 晨会稿 → 点名 → 会议 → 报告" />
    <a-steps :current="current" size="small" responsive>
      <a-step title="会前录入" />
      <a-step title="晨会稿" />
      <a-step title="点名" />
      <a-step title="会议" />
      <a-step title="报告" />
    </a-steps>
    <MeetingInfoStep v-if="current === 0" :loading="loading" @submit="handleCreate" />
    <SpeechDraftStep
      v-else-if="current === 1"
      :meeting-id="meeting!.id"
      @generated="handleSpeechGenerated"
    />
    <AttendanceStep
      v-else-if="current === 2"
      :meeting-id="meeting!.id"
      @done="current = 3"
    />
    <MeetingStep
      v-else-if="current === 3"
      :meeting-id="meeting!.id"
      @done="handleMeetingDone"
    />
    <ReportStep v-else-if="current === 4" :meeting-id="meeting!.id" />
  </div>
</template>

<style scoped lang="less">
.ai-meeting-page {
  max-width: 720px;
  margin: 0 auto;
  padding: @page-padding;
}
.ai-meeting-page :deep(.ant-steps) {
  margin-bottom: @spacing-lg;
}
</style>
```

- [ ] **Step 2: typecheck 并 Commit**

Run: `pnpm run typecheck`
Expected: 组件未创建导致的报错属预期，创建 2.7–2.11 各组件后消除。
```bash
git add user-web/src/views/ai-meeting
git commit -m "feat: AI晨会分步向导骨架"
```

### Task 2.7：Step1 会前录入

**Files:**
- Create: `user-web/src/views/ai-meeting/components/MeetingInfoStep.vue`

- [ ] **Step 1: 创建表单组件（props down / events up）**

```vue
<script setup lang="ts">
import { reactive } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { PreInfo } from '@/types'

defineProps<{ loading: boolean }>()
const emit = defineEmits<{ submit: [preInfo: PreInfo] }>()

const form = reactive<PreInfo>({
  date: new Date().toISOString().slice(0, 10),
  weather: '',
  tasks: '',
  riskPoints: '',
})

function onSubmit(): void {
  emit('submit', { ...form })
}
</script>

<template>
  <SectionCard title="会前录入" flush>
    <a-form layout="vertical">
      <a-form-item label="日期"><a-date-picker v-model:value="form.date" value-format="YYYY-MM-DD" style="width: 100%" /></a-form-item>
      <a-form-item label="天气"><a-input v-model:value="form.weather" placeholder="如：晴，28℃" /></a-form-item>
      <a-form-item label="今日任务"><a-textarea v-model:value="form.tasks" :rows="3" placeholder="今日施工任务" /></a-form-item>
      <a-form-item label="风险点"><a-textarea v-model:value="form.riskPoints" :rows="3" placeholder="安全风险提示" /></a-form-item>
      <AppButton variant="primary" size="lg" block :loading="loading" @click="onSubmit">
        保存并生成晨会稿
      </AppButton>
    </a-form>
  </SectionCard>
</template>
```

- [ ] **Step 2: typecheck 并 Commit**

```bash
git add user-web/src/views/ai-meeting/components/MeetingInfoStep.vue
git commit -m "feat: AI晨会会前录入步骤"
```

### Task 2.8：Step2 晨会稿生成与审核

**Files:**
- Create: `user-web/src/views/ai-meeting/components/SpeechDraftStep.vue`

- [ ] **Step 1: 创建晨会稿组件**

```vue
<script setup lang="ts">
import { ref } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { SpeechDraftDto } from '@/types'
import { generateSpeech, saveSpeechDraft } from '@/api/modules/aiMeeting'
import { useAudioPlayer } from '../composables/useAudioPlayer'

const props = defineProps<{ meetingId: string }>()
const emit = defineEmits<{ generated: [] }>()

const draft = ref<SpeechDraftDto | null>(null)
const loading = ref(false)
const editing = ref(false)
const { playing, stop } = useAudioPlayer()

async function onGenerate(): Promise<void> {
  loading.value = true
  try {
    draft.value = await generateSpeech(props.meetingId)
  } finally {
    loading.value = false
  }
}

async function onSave(): Promise<void> {
  if (!draft.value) return
  draft.value = await saveSpeechDraft(props.meetingId, draft.value.content)
  editing.value = false
  emit('generated')
}
</script>

<template>
  <SectionCard title="晨会稿" flush>
    <AppButton v-if="!draft" variant="primary" block :loading="loading" @click="onGenerate">
      生成晨会稿
    </AppButton>
    <template v-else>
      <a-textarea
        v-model:value="draft.content"
        :disabled="!editing"
        :rows="10"
      />
      <div class="speech-draft-step__actions">
        <AppButton size="sm" @click="editing = !editing">{{ editing ? '取消编辑' : '编辑' }}</AppButton>
        <AppButton size="sm" @click="() => (editing ? onSave() : undefined)" :disabled="!editing">
          保存
        </AppButton>
        <AppButton size="sm" @click="() => (playing ? stop() : undefined)">停止播放</AppButton>
        <AppButton variant="primary" size="lg" block @click="onSave">
          确认并开始点名
        </AppButton>
      </div>
    </template>
  </SectionCard>
</template>

<style scoped lang="less">
.speech-draft-step__actions {
  display: flex;
  gap: @spacing-sm;
  margin-top: @spacing-md;
}
</style>
```

说明：TTS 播放依赖 Phase 3 的 `POST /api/meeting/records/{id}/qa/audio` 链路复用；v1 在 Phase 2 阶段仅展示文本，播放按钮在 Task 3.7 完成后接入。

- [ ] **Step 2: typecheck 并 Commit**

```bash
git add user-web/src/views/ai-meeting/components/SpeechDraftStep.vue
git commit -m "feat: AI晨会晨会稿步骤"
```

### Task 2.9：Step3 点名

**Files:**
- Create: `user-web/src/views/ai-meeting/components/AttendanceStep.vue`

- [ ] **Step 1: 创建点名组件（拍照上传 + 列表 + 补扫）**

```vue
<script setup lang="ts">
import { ref, onMounted, onScopeDispose } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { AttendanceItemDto } from '@/types'
import { recognizeAttendance, getAttendance } from '@/api/modules/aiMeeting'
import { useCamera } from '../composables/useCamera'

const props = defineProps<{ meetingId: string }>()
const emit = defineEmits<{ done: [] }>()

const videoRef = ref<HTMLVideoElement | null>(null)
const { stream, error, start, stop, capturePhoto } = useCamera()
const list = ref<AttendanceItemDto[]>([])
const loading = ref(false)

onMounted(() => start())
onScopeDispose(() => stop())

async function onCapture(): Promise<void> {
  if (!videoRef.value || !stream.value) return
  loading.value = true
  try {
    const photo = await capturePhoto(videoRef.value)
    await recognizeAttendance(props.meetingId, photo)
    list.value = await getAttendance(props.meetingId)
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <SectionCard title="现场点名" flush>
    <video
      v-if="stream"
      ref="videoRef"
      class="attendance-step__video"
      autoplay
      playsinline
    />
    <a-result v-else-if="error" status="warning" title="无法访问摄像头" :sub-title="error" />
    <div class="attendance-step__actions">
      <AppButton variant="primary" block :loading="loading" @click="onCapture">
        拍照识别（手动支架扫一圈，多拍几次自动去重）
      </AppButton>
    </div>
    <a-table
      size="small"
      row-key="workerId"
      :columns="[
        { title: '姓名', dataIndex: 'name' },
        { title: '班组', dataIndex: 'team' },
        { title: '状态', dataIndex: 'status', width: 110 },
        { title: '置信度', dataIndex: 'confidence', width: 100 },
      ]"
      :data-source="list"
      :pagination="false"
    />
    <AppButton v-if="list.length" variant="primary" size="lg" block @click="emit('done')">
      点名完成，进入会议
    </AppButton>
  </SectionCard>
</template>

<style scoped lang="less">
.attendance-step__video {
  width: 100%;
  border-radius: @radius-md;
}
.attendance-step__actions {
  margin: @spacing-md 0;
}
</style>
```

- [ ] **Step 2: typecheck 并 Commit**

```bash
git add user-web/src/views/ai-meeting/components/AttendanceStep.vue
git commit -m "feat: AI晨会点名步骤"
```

### Task 2.10：Step4 会议（录音 + 按住说话问答）

**Files:**
- Create: `user-web/src/views/ai-meeting/components/MeetingStep.vue`

- [ ] **Step 1: 创建会议组件**

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { QaRecordDto } from '@/types'
import { askQa, askQaAudio, uploadMeetingRecording } from '@/api/modules/aiMeeting'
import { useRecorder } from '../composables/useRecorder'

const props = defineProps<{ meetingId: string }>()
const emit = defineEmits<{ done: [] }>()

const { recording: pttRecording, start: startPtt, stop: stopPtt } = useRecorder()
const { recording: meetingRecording, start: startMeetingRec, stop: stopMeetingRec } = useRecorder()
const qaList = ref<QaRecordDto[]>([])
const question = ref('')
const loading = ref(false)

onMounted(() => {
  void startMeetingRec()
})

async function onAskText(): Promise<void> {
  if (!question.value.trim()) return
  loading.value = true
  try {
    qaList.value.push(await askQa(props.meetingId, question.value.trim()))
    question.value = ''
  } finally {
    loading.value = false
  }
}

async function onPttPress(): Promise<void> {
  await startPtt()
}

async function onPttRelease(): Promise<void> {
  const audio = await stopPtt()
  if (audio.size === 0) return
  loading.value = true
  try {
    const rec = await askQaAudio(props.meetingId, audio)
    qaList.value.push(rec)
  } finally {
    loading.value = false
  }
}

async function onFinish(): Promise<void> {
  loading.value = true
  try {
    const audio = await stopMeetingRec()
    if (audio.size > 0) {
      await uploadMeetingRecording(props.meetingId, audio)
    }
    emit('done')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <SectionCard title="会议进行中" flush>
    <a-alert type="info" show-icon :message="meetingRecording ? '全程录音中…按住下方按钮提问，松开后自动识别并回答' : '录音未开始'" />
    <AppButton
      variant="primary"
      size="lg"
      block
      :loading="loading"
      @pointerdown="onPttPress"
      @pointerup="onPttRelease"
    >
      {{ pttRecording ? '松开发问' : '按住说话' }}
    </AppButton>
    <a-input-search
      v-model:value="question"
      placeholder="也可以输入文字提问"
      enter-button="提问"
      @search="onAskText"
    />
    <div class="meeting-step__qa">
      <div v-for="qa in qaList" :key="qa.id" class="meeting-step__qa-item">
        <div><b>问：</b>{{ qa.question }}</div>
        <div><b>答：</b>{{ qa.answer }}</div>
      </div>
    </div>
    <AppButton variant="primary" size="lg" block :loading="loading" @click="onFinish">
      结束会议并生成报告
    </AppButton>
  </SectionCard>
</template>

<style scoped lang="less">
.meeting-step__qa {
  margin-top: @spacing-md;
}
.meeting-step__qa-item {
  padding: @spacing-sm @spacing-md;
  background: @content-bg;
  border-radius: @radius-md;
  margin-bottom: @spacing-sm;
}
</style>
```

- [ ] **Step 2: typecheck 并 Commit**

```bash
git add user-web/src/views/ai-meeting/components/MeetingStep.vue
git commit -m "feat: AI晨会会议步骤（录音与按住说话问答）"
```

### Task 2.11：Step5 报告

**Files:**
- Create: `user-web/src/views/ai-meeting/components/ReportStep.vue`

- [ ] **Step 1: 创建报告组件**

```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import type { ReportDto } from '@/types'
import { getReport } from '@/api/modules/aiMeeting'

const props = defineProps<{ meetingId: string }>()
const report = ref<ReportDto | null>(null)

onMounted(async () => {
  report.value = await getReport(props.meetingId)
})
</script>

<template>
  <SectionCard title="会后报告" flush>
    <a-skeleton v-if="!report" :paragraph="{ rows: 6 }" />
    <template v-else>
      <div class="report-step__block">
        <div class="label">出勤</div>
        <a-table
          size="small"
          row-key="workerId"
          :columns="[
            { title: '姓名', dataIndex: 'name' },
            { title: '班组', dataIndex: 'team' },
            { title: '状态', dataIndex: 'status', width: 110 },
          ]"
          :data-source="report.attendance"
          :pagination="false"
        />
      </div>
      <div class="report-step__block">
        <div class="label">转写稿</div>
        <a-typography-paragraph>{{ report.transcript }}</a-typography-paragraph>
      </div>
      <div class="report-step__block">
        <div class="label">问答记录</div>
        <div v-for="qa in report.qaRecords" :key="qa.id">
          <b>问：</b>{{ qa.question }}<br />
          <b>答：</b>{{ qa.answer }}
        </div>
      </div>
    </template>
  </SectionCard>
</template>

<style scoped lang="less">
.report-step__block {
  margin-bottom: @spacing-lg;
}
</style>
```

- [ ] **Step 2: typecheck 并 Commit**

```bash
git add user-web/src/views/ai-meeting/components/ReportStep.vue
git commit -m "feat: AI晨会报告步骤"
```

### Task 2.12：媒体 composables 与单测

**Files:**
- Create: `user-web/src/views/ai-meeting/composables/useCamera.ts`
- Create: `user-web/src/views/ai-meeting/composables/useRecorder.ts`
- Create: `user-web/src/views/ai-meeting/composables/useAudioPlayer.ts`
- Create: `user-web/__tests__/ai-meeting-media.test.ts`

- [ ] **Step 1: 写失败测试（mock 浏览器媒体 API）**

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useRecorder } from '@/views/ai-meeting/composables/useRecorder'
import { useAudioPlayer } from '@/views/ai-meeting/composables/useAudioPlayer'

class FakeMediaRecorder {
  ondataavailable: ((e: { data: Blob }) => void) | null = null
  onstop: (() => void) | null = null
  start(): void {}
  stop(): void {
    this.onstop?.()
  }
}

beforeEach(() => {
  Object.defineProperty(navigator, 'mediaDevices', {
    value: { getUserMedia: vi.fn().mockResolvedValue({ getTracks: () => [] }) },
    configurable: true,
  })
  vi.stubGlobal('MediaRecorder', FakeMediaRecorder)
})

describe('useRecorder', () => {
  it('start 后 recording 为 true，stop 后为 false', async () => {
    const rec = useRecorder()
    await rec.start()
    expect(rec.recording.value).toBe(true)
    await rec.stop()
    expect(rec.recording.value).toBe(false)
  })
})

describe('useAudioPlayer', () => {
  it('play 后 playing 为 true', () => {
    class FakeAudio {
      src = ''
      onended: (() => void) | null = null
      play = vi.fn()
      pause = vi.fn()
      currentTime = 0
    }
    vi.stubGlobal('Audio', FakeAudio)
    vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:fake'), revokeObjectURL: vi.fn() })
    const player = useAudioPlayer()
    player.play(new Blob(['x']))
    expect(player.playing.value).toBe(true)
  })
})
```

- [ ] **Step 2: 运行确认失败**

Run: `pnpm --filter user-web test -- ai-meeting-media`
Expected: FAIL（composables 不存在）。

- [ ] **Step 3: 实现三个 composable**

`useCamera.ts`：
```ts
import { ref } from 'vue'

export function useCamera() {
  const stream = ref<MediaStream | null>(null)
  const error = ref<string | null>(null)

  async function start(): Promise<void> {
    try {
      stream.value = await navigator.mediaDevices.getUserMedia({ video: true, audio: false })
    } catch (e) {
      error.value = e instanceof Error ? e.message : '无法访问摄像头'
    }
  }

  function stop(): void {
    stream.value?.getTracks().forEach((t) => t.stop())
    stream.value = null
  }

  async function capturePhoto(video: HTMLVideoElement): Promise<Blob> {
    const canvas = document.createElement('canvas')
    canvas.width = video.videoWidth
    canvas.height = video.videoHeight
    const ctx = canvas.getContext('2d')
    if (!ctx) throw new Error('canvas 不可用')
    ctx.drawImage(video, 0, 0)
    return new Promise<Blob>((resolve, reject) =>
      canvas.toBlob((b) => (b ? resolve(b) : reject(new Error('截图失败'))), 'image/jpeg', 0.92),
    )
  }

  return { stream, error, start, stop, capturePhoto }
}
```

`useRecorder.ts`：
```ts
import { ref } from 'vue'

export function useRecorder() {
  const stream = ref<MediaStream | null>(null)
  const recorder = ref<MediaRecorder | null>(null)
  const chunks = ref<Blob[]>([])
  const recording = ref(false)

  async function start(): Promise<void> {
    stream.value = await navigator.mediaDevices.getUserMedia({ audio: true, video: false })
    const rec = new MediaRecorder(stream.value)
    chunks.value = []
    rec.ondataavailable = (e) => {
      if (e.data.size > 0) chunks.value.push(e.data)
    }
    rec.start()
    recorder.value = rec
    recording.value = true
  }

  function stop(): Promise<Blob> {
    return new Promise((resolve) => {
      const rec = recorder.value
      if (!rec) {
        resolve(new Blob())
        return
      }
      rec.onstop = () => {
        stream.value?.getTracks().forEach((t) => t.stop())
        recording.value = false
        resolve(new Blob(chunks.value, { type: 'audio/webm' }))
      }
      rec.stop()
    })
  }

  return { recording, start, stop }
}
```

`useAudioPlayer.ts`：
```ts
import { ref } from 'vue'

export function useAudioPlayer() {
  const audio = new Audio()
  const playing = ref(false)
  let currentUrl: string | null = null

  function play(blob: Blob): void {
    stop()
    currentUrl = URL.createObjectURL(blob)
    audio.src = currentUrl
    void audio.play()
    playing.value = true
    audio.onended = () => {
      playing.value = false
      if (currentUrl) URL.revokeObjectURL(currentUrl)
      currentUrl = null
    }
  }

  function stop(): void {
    audio.pause()
    audio.currentTime = 0
    if (currentUrl) URL.revokeObjectURL(currentUrl)
    currentUrl = null
    playing.value = false
  }

  return { playing, play, stop }
}
```

- [ ] **Step 4: 运行测试与 typecheck**

Run: `pnpm --filter user-web test -- ai-meeting-media`
Run: `pnpm run typecheck`
Expected: 全 PASS、无类型错误。

- [ ] **Step 5: Commit**

```bash
git add user-web/src/views/ai-meeting/composables user-web/__tests__/ai-meeting-media.test.ts
git commit -m "feat: AI晨会媒体 composables 与单测"
```

### Task 2.13：前端收尾验证

- [ ] **Step 1: 运行全量检查**

Run: `pnpm run typecheck`
Run: `pnpm --filter user-web test`
Expected: 全部通过。

- [ ] **Step 2: 浏览器手工验证（mock 模式）**

Run: `pnpm --filter user-web dev`
在浏览器打开 `/ai-meeting`，依次走完 5 步；用电脑摄像头/麦克风验证拍照识别、按住说话、报告展示。发现的问题记录并修复后重跑 typecheck。

- [ ] **Step 3: Commit 修复（如有）**

```bash
git add user-web
git commit -m "fix: AI晨会前端联调修复"
```

## Phase 3：ABP 后端（依赖 Phase 1 与 Phase 2）

领域代码位于 `backend/DredgeAI.BidCompare/src/`，按现有 CompareTasks 模块分层组织。

### Task 3.1：领域实体与枚举

**Files:**
- Create: `Domain.Shared/MeetingBot/MeetingStatus.cs`
- Create: `Domain/MeetingBot/MeetingRecord.cs`
- Create: `Domain/MeetingBot/SpeechDraft.cs`
- Create: `Domain/MeetingBot/AttendanceRecord.cs`
- Create: `Domain/MeetingBot/QaRecord.cs`
- Create: `Domain/MeetingBot/WorkerProfile.cs`

- [ ] **Step 1: 创建枚举**

```csharp
namespace DredgeAI.BidCompare.MeetingBot
{
    public enum MeetingStatus
    {
        Draft = 0,
        Prepared = 1,
        Rollcall = 2,
        Ongoing = 3,
        Completed = 4
    }
}
```

- [ ] **Step 2: 创建聚合根 MeetingRecord**

```csharp
using System;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities.Auditing;

namespace DredgeAI.BidCompare.MeetingBot
{
    public class MeetingRecord : FullAuditedAggregateRoot<Guid>
    {
        public DateTime Date { get; set; }
        public string PreInfoJson { get; set; } = "{}";
        public MeetingStatus Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public Guid? SpeechDraftId { get; set; }
        public string? TranscriptFile { get; set; }
        public string? ReportFile { get; set; }
        public ICollection<AttendanceRecord> Attendance { get; set; } = new List<AttendanceRecord>();
        public ICollection<QaRecord> QaRecords { get; set; } = new List<QaRecord>();
    }
}
```

- [ ] **Step 3: 创建其余实体**

`SpeechDraft`：`Id(Guid)`、`MeetingRecordId`、`Content`、`Status(draft/generated/confirmed)`、`GeneratedAt`、`EditedAt`。
`AttendanceRecord`：`Id(Guid)`、`MeetingRecordId`、`WorkerId(Guid?)`、`Name`、`Team`、`Status(present/absent/late/unrecognized)`、`Confidence(double)`、`RecognizedAt`、`PhotoFile(string?)`。
`QaRecord`：`Id(Guid)`、`MeetingRecordId`、`QuestionText`、`AnswerText`、`IntentType(knowledge/chitchat/meeting)`、`SourcesJson`、`AudioFile(string?)`、`CreatedAt`。
`WorkerProfile`：`Id(Guid)`、`Name`、`EmployeeNo`、`Team`、`FaceStatus(enrolled/pending)`、`FacePhotosJson`、`Active`。

- [ ] **Step 4: 在 DbContext 注册 DbSet 并配置表**

在 `BidCompareDbContext` 增加 5 个 DbSet，并配置 `MeetingRecord` 与 `AttendanceRecord`、`QaRecord` 的一对多关系、`WorkerProfile.EmployeeNo` 唯一索引。

- [ ] **Step 5: 生成迁移**

Run（在 `backend/DredgeAI.BidCompare` 目录）：
```bash
dotnet ef migrations add AddMeetingBot -p src/DredgeAI.BidCompare.EntityFrameworkCore -s src/DredgeAI.BidCompare.DbMigrator
dotnet run --project src/DredgeAI.BidCompare.DbMigrator
```
Expected: 迁移生成并应用成功。

- [ ] **Step 6: Commit**

```bash
git add backend/DredgeAI.BidCompare
git commit -m "feat: MeetingBot 领域实体与迁移"
```

### Task 3.2：DTO 与应用服务接口

**Files:**
- Create: `Application.Contracts/MeetingBot/MeetingRecordDto.cs`
- Create: `Application.Contracts/MeetingBot/IMeetingRecordAppService.cs`
- Create: `Application.Contracts/MeetingBot/WorkerProfileDto.cs`
- Create: `Application.Contracts/MeetingBot/IWorkerProfileAppService.cs`

- [ ] **Step 1: 创建 DTO**

```csharp
using System;
using System.Collections.Generic;

namespace DredgeAI.BidCompare.MeetingBot
{
    public class PreInfoInput
    {
        public DateTime Date { get; set; }
        public string Weather { get; set; } = "";
        public string Tasks { get; set; } = "";
        public string RiskPoints { get; set; } = "";
    }

    public class MeetingRecordDto
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public string PreInfoJson { get; set; } = "{}";
        public MeetingStatus Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public SpeechDraftDto? SpeechDraft { get; set; }
        public List<AttendanceItemDto> Attendance { get; set; } = new();
        public List<QaRecordDto> QaRecords { get; set; } = new();
        public ReportDto? Report { get; set; }
    }

    public class SpeechDraftDto
    {
        public Guid Id { get; set; }
        public string Content { get; set; } = "";
        public string Status { get; set; } = "draft";
        public DateTime UpdatedAt { get; set; }
    }

    public class AttendanceItemDto
    {
        public Guid? WorkerId { get; set; }
        public string Name { get; set; } = "";
        public string Team { get; set; } = "";
        public string Status { get; set; } = "unrecognized";
        public double Confidence { get; set; }
    }

    public class QaRecordDto
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = "";
        public string Answer { get; set; } = "";
        public string IntentType { get; set; } = "chitchat";
        public List<string> Sources { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }

    public class ReportDto
    {
        public Guid Id { get; set; }
        public string Transcript { get; set; } = "";
        public List<AttendanceItemDto> Attendance { get; set; } = new();
        public List<QaRecordDto> QaRecords { get; set; } = new();
        public DateTime CreatedAt { get; set; }
    }
}
```

- [ ] **Step 1.5: 补充输入 DTO（追加到 MeetingRecordDto.cs 的命名空间内）**

```csharp
public class UpdateSpeechInput
{
    public string Content { get; set; } = "";
}

public class AskQaInput
{
    public string Question { get; set; } = "";
}
```

- [ ] **Step 2: 创建应用服务接口**

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace DredgeAI.BidCompare.MeetingBot
{
    public interface IMeetingRecordAppService : IApplicationService
    {
        Task<MeetingRecordDto> CreateAsync(PreInfoInput input);
        Task<SpeechDraftDto?> GetSpeechAsync(Guid id);
        Task<SpeechDraftDto> GenerateSpeechAsync(Guid id);
        Task<SpeechDraftDto> UpdateSpeechAsync(Guid id, string content);
        Task<MeetingRecordDto> StartAsync(Guid id);
        Task<List<AttendanceItemDto>> RecognizeAttendanceAsync(Guid id, byte[] image);
        Task<List<AttendanceItemDto>> GetAttendanceAsync(Guid id);
        Task<QaRecordDto> AskQaAsync(Guid id, string question);
        Task<MeetingRecordDto> SaveRecordingAsync(Guid id, byte[] audio, string fileName);
        Task<MeetingRecordDto> CompleteAsync(Guid id);
        Task<ReportDto?> GetReportAsync(Guid id);
    }
}
```

`IWorkerProfileAppService`：`Task ImportAsync(Stream file)`（花名册照片批量导入，xlsx/zip）、`Task UpdateFaceAsync(Guid workerId, byte[] image)`。

- [ ] **Step 3: Commit**

```bash
git add backend/DredgeAI.BidCompare
git commit -m "feat: MeetingBot DTO 与应用服务接口"
```

### Task 3.3：MeetingBot HTTP 客户端

**Files:**
- Create: `Domain/MeetingBot/IMeetingBotClient.cs`
- Create: `Application/MeetingBot/MeetingBotClient.cs`

- [ ] **Step 1: 定义客户端接口**

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DredgeAI.BidCompare.MeetingBot
{
    public interface IMeetingBotClient
    {
        Task<string> AsrAsync(byte[] audio, CancellationToken ct = default);
        Task<byte[]> TtsAsync(string text, CancellationToken ct = default);
        Task<List<FaceMatchDto>> RecognizeAsync(byte[] image, CancellationToken ct = default);
        Task<int> CountAsync(byte[] image, CancellationToken ct = default);
        Task<string> TranscribeAsync(byte[] audio, CancellationToken ct = default);
    }

    public class FaceMatchDto
    {
        public string? WorkerId { get; set; }
        public string? Name { get; set; }
        public double Confidence { get; set; }
    }
}
```

- [ ] **Step 2: 实现客户端（IHttpClientFactory 注册）**

`Application/MeetingBot/MeetingBotClient.cs` 用 `HttpClient` 调 `MEETING_BOT_BASE_URL`（配置节 `MeetingBot:BaseUrl`），每个请求带 `X-Meeting-Bot-Key`（`MeetingBot:Key`）；`AsrAsync` 用 `MultipartFormDataContent` 上传 audio，`TtsAsync` POST JSON `{"text": ...}` 取字节，`RecognizeAsync` 上传 image 解析 `faces` 数组，`CountAsync` 解析 `count`，`TranscribeAsync` 上传后轮询 `GET /transcribe/{jobId}` 直到 `done`（超时 120s）。

- [ ] **Step 3: 在模块注册 typed client**

`BidCompareApplicationModule` 的 `ConfigureServices` 增加：
```csharp
context.Services.AddHttpClient<IMeetingBotClient, MeetingBotClient>((sp, http) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    http.BaseAddress = new Uri(cfg["MeetingBot:BaseUrl"] ?? "http://localhost:8101");
    http.DefaultRequestHeaders.Add("X-Meeting-Bot-Key", cfg["MeetingBot:Key"] ?? "");
});
```

- [ ] **Step 4: 写入 appsettings.Development.json**

```json
{ "MeetingBot": { "BaseUrl": "http://localhost:8101", "Key": "dev-key" } }
```

- [ ] **Step 5: Commit**

```bash
git add backend/DredgeAI.BidCompare
git commit -m "feat: MeetingBot HTTP 客户端"
```

### Task 3.4：晨会稿生成编排

**Files:**
- Create: `Application/MeetingBot/MeetingRecordAppService.cs`
- Modify: `Application/MeetingBot/BidCompareApplicationAutoMapperProfile.cs`

- [ ] **Step 1: 先读现有集成接口**

阅读 `Domain/AnGineer` 下 AnGIneer 客户端接口与 `Application/AI` 下 `ILlmGateway`，确认检索方法（无则新增 `SearchAsync(string query)` 返回命中文本列表）与 LLM 调用方法签名。

- [ ] **Step 2: 实现服务骨架与 CreateAsync**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Json;

namespace DredgeAI.BidCompare.MeetingBot
{
    public class MeetingRecordAppService : ApplicationService, IMeetingRecordAppService
    {
        private readonly IRepository<MeetingRecord, Guid> _meetings;
        private readonly IRepository<SpeechDraft, Guid> _drafts;
        private readonly IRepository<AttendanceRecord, Guid> _attendance;
        private readonly IRepository<QaRecord, Guid> _qa;
        private readonly IMeetingBotClient _bot;
        private readonly IJsonSerializer _json;

        public MeetingRecordAppService(
            IRepository<MeetingRecord, Guid> meetings,
            IRepository<SpeechDraft, Guid> drafts,
            IRepository<AttendanceRecord, Guid> attendance,
            IRepository<QaRecord, Guid> qa,
            IMeetingBotClient bot,
            IJsonSerializer json)
        {
            _meetings = meetings;
            _drafts = drafts;
            _attendance = attendance;
            _qa = qa;
            _bot = bot;
            _json = json;
        }

        public async Task<MeetingRecordDto> CreateAsync(PreInfoInput input)
        {
            var preInfo = new
            {
                input.Date,
                input.Weather,
                input.Tasks,
                input.RiskPoints
            };
            var meeting = new MeetingRecord
            {
                Date = input.Date,
                PreInfoJson = _json.Serialize(preInfo),
                Status = MeetingStatus.Draft
            };
            await _meetings.InsertAsync(meeting);
            return await GetAsync(meeting.Id);
        }

        private async Task<MeetingRecordDto> GetAsync(Guid id)
        {
            var meeting = await _meetings.GetAsync(id, includeDetails: true);
            var dto = ObjectMapper.Map<MeetingRecord, MeetingRecordDto>(meeting);
            dto.SpeechDraft = meeting.SpeechDraftId.HasValue
                ? ObjectMapper.Map<SpeechDraft, SpeechDraftDto>(await _drafts.GetAsync(meeting.SpeechDraftId.Value))
                : null;
            return dto;
        }
    }
}
```

- [ ] **Step 3: 实现 GenerateSpeechAsync**

构造函数按 Step 1 读到的接口类型补充注入 AnGIneer 客户端与 `ILlmGateway`。读取会议与 PreInfo → 调用 AnGIneer 检索（query 由 "晨会安全交底、今日任务" 组合）取前 5 条文本 → 拼 prompt（系统提示 + 检索证据 + 前置信息，要求输出晨会稿结构）→ 调 `ILlmGateway` 生成 → 保存 `SpeechDraft`（Status=generated）→ 返回 DTO。

- [ ] **Step 3.5: 实现其余服务方法**

`GetSpeechAsync`：按 `meeting.SpeechDraftId` 返回草稿或 null；`UpdateSpeechAsync`：更新 Content、Status=confirmed、EditedAt=now；`StartAsync`：Status=Rollcall、StartedAt=now；`SaveRecordingAsync`：用现有 `IFileStorage` 保存录音（构造函数补充注入），`TranscriptFile` 存文件路径。`GenerateSpeechAsync` 中 AnGIneer 检索失败时降级为不带证据的纯 LLM 生成（答案标注"无知识库证据"）。

- [ ] **Step 4: 写单元测试（mock 检索与 LLM）**

在 `test/DredgeAI.BidCompare.Application.Tests` 新增 `MeetingRecordAppService_Tests`：mock `IMeetingBotClient`、AnGIneer 客户端、`ILlmGateway`，断言 CreateAsync 返回 Draft、GenerateSpeechAsync 生成内容包含前置信息中的任务文本。

- [ ] **Step 5: 运行测试**

Run: `dotnet test backend/DredgeAI.BidCompare/test/DredgeAI.BidCompare.Application.Tests`
Expected: PASS。

- [ ] **Step 6: Commit**

```bash
git add backend/DredgeAI.BidCompare
git commit -m "feat: MeetingBot 晨会稿生成编排"
```

### Task 3.5：工人人脸库管理（批量导入 + 现场补录）

**Files:**
- Create: `Application/MeetingBot/WorkerProfileAppService.cs`
- Create: `HttpApi/Controllers/WorkerProfileController.cs`
- Modify: `Domain/MeetingBot/IMeetingBotClient.cs`（增加 EnrollAsync）

- [ ] **Step 1: IMeetingBotClient 增加 EnrollAsync**

在接口与 `MeetingBotClient` 实现中增加 `Task EnrollAsync(string workerId, byte[] image, CancellationToken ct = default)`，调 meeting-bot `POST /enroll`（Task 1.7 已实现端点）。

- [ ] **Step 2: 实现 WorkerProfileAppService**

`ImportAsync(Stream file)`：解析 xlsx/zip 花名册（姓名/工号/班组/照片），逐行创建 `WorkerProfile`（FaceStatus=pending）并调 `EnrollAsync`，成功置 enrolled；`UpdateFaceAsync(workerId, image)`：调 `EnrollAsync` 并置 enrolled、保存 FacePhotosJson；`GetListAsync()`：返回 WorkerDto 列表。

- [ ] **Step 3: 创建 WorkerProfileController**

`AbpControllerBase`，`[Area("meeting")]`，`[Route("api/meeting/workers")]`：`GET`（列表）、`POST import`（multipart，含文件）、`POST {id:guid}/face`（multipart 照片）。

- [ ] **Step 4: 写单元测试**

断言：导入成功状态流转 pending→enrolled；补录后 FaceStatus=enrolled。

- [ ] **Step 5: 运行测试并 Commit**

```bash
git add backend/DredgeAI.BidCompare
git commit -m "feat: MeetingBot 工人人脸库管理"
```

### Task 3.6：点名编排

- [ ] **Step 1: 实现 RecognizeAttendanceAsync**

`image` → `_bot.RecognizeAsync(image)` → 阈值 0.6 过滤；已有 `AttendanceRecord`（同 MeetingRecordId + WorkerId）跳过（去重）；命中者 Status=present；未命中者收集为 unrecognized 条目（Name="未识别"）。全部落库后返回列表。

- [ ] **Step 2: 实现 GetAttendanceAsync**

按会议查询 AttendanceRecord 列表，映射为 DTO（缺勤判定：v1 由花名册应到名单减去已识别，先返回已识别 + 未识别即可）。

- [ ] **Step 3: 写单元测试（去重与阈值）**

断言：同一 WorkerId 两次识别只产生一条记录；confidence < 0.6 归入 unrecognized。

- [ ] **Step 4: 运行测试并 Commit**

```bash
git add backend/DredgeAI.BidCompare
git commit -m "feat: MeetingBot 点名编排"
```

### Task 3.7：问答编排（文本 + 音频）

**Files:**
- Create: `HttpApi/Controllers/MeetingRecordController.cs`

- [ ] **Step 1: 实现 AskQaAsync（文本）**

规则意图分级：问题含「规范/安全/要求/作业/交底」→ knowledge，否则 chitchat。knowledge 走 AnGIneer 检索 + `ILlmGateway` 生成（答案带证据），chitchat 直接 `ILlmGateway`；AnGIneer 检索失败时降级为不带证据的 LLM 直答。保存 `QaRecord`（SourcesJson 存证据文件名/页码），返回 DTO。

- [ ] **Step 2: 创建 MeetingRecordController（AbpControllerBase，路由与前端 urls.ts 一致）**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DredgeAI.BidCompare.MeetingBot;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;

namespace DredgeAI.BidCompare.Controllers;

[Area("meeting")]
[Route("api/meeting/records")]
public class MeetingRecordController : AbpControllerBase
{
    private readonly IMeetingRecordAppService _service;
    private readonly IMeetingBotClient _bot;

    public MeetingRecordController(IMeetingRecordAppService service, IMeetingBotClient bot)
    {
        _service = service;
        _bot = bot;
    }

    [HttpPost]
    public Task<MeetingRecordDto> Create([FromBody] PreInfoInput input) => _service.CreateAsync(input);

    [HttpPost("{id:guid}/speech/generate")]
    public Task<SpeechDraftDto> GenerateSpeech(Guid id) => _service.GenerateSpeechAsync(id);

    [HttpGet("{id:guid}/speech")]
    public Task<SpeechDraftDto?> GetSpeech(Guid id) => _service.GetSpeechAsync(id);

    [HttpPut("{id:guid}/speech")]
    public Task<SpeechDraftDto> UpdateSpeech(Guid id, [FromBody] UpdateSpeechInput input) =>
        _service.UpdateSpeechAsync(id, input.Content);

    [HttpPost("{id:guid}/start")]
    public Task<MeetingRecordDto> Start(Guid id) => _service.StartAsync(id);

    [HttpPost("{id:guid}/attendance/recognize")]
    public async Task<List<AttendanceItemDto>> Recognize(Guid id, [FromForm] IFormFile image)
    {
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);
        return await _service.RecognizeAttendanceAsync(id, ms.ToArray());
    }

    [HttpGet("{id:guid}/attendance")]
    public Task<List<AttendanceItemDto>> Attendance(Guid id) => _service.GetAttendanceAsync(id);

    [HttpPost("{id:guid}/qa")]
    public Task<QaRecordDto> AskQa(Guid id, [FromBody] AskQaInput input) => _service.AskQaAsync(id, input.Question);

    [HttpPost("{id:guid}/qa/audio")]
    public async Task<IActionResult> AskQaAudio(Guid id, [FromForm] IFormFile audio)
    {
        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        var text = await _bot.AsrAsync(ms.ToArray());
        var record = await _service.AskQaAsync(id, text);
        var voice = await _bot.TtsAsync(record.Answer);
        return File(voice, "audio/wav", $"qa-{record.Id}.wav");
    }

    [HttpPost("{id:guid}/recording")]
    public async Task<MeetingRecordDto> SaveRecording(Guid id, [FromForm] IFormFile audio)
    {
        using var ms = new MemoryStream();
        await audio.CopyToAsync(ms);
        return await _service.SaveRecordingAsync(id, ms.ToArray(), audio.FileName);
    }

    [HttpPost("{id:guid}/complete")]
    public Task<MeetingRecordDto> Complete(Guid id) => _service.CompleteAsync(id);

    [HttpGet("{id:guid}/report")]
    public Task<ReportDto?> Report(Guid id) => _service.GetReportAsync(id);
}
```

- [ ] **Step 3: 写单元测试（意图分级）**

断言：含"规范"的问题 intentType=knowledge；"你好" → chitchat。

- [ ] **Step 4: 运行测试并 Commit**

```bash
git add backend/DredgeAI.BidCompare
git commit -m "feat: MeetingBot 问答编排（文本+音频）"
```

### Task 3.8：会后转写与报告

**Files:**
- Create: `Application/BackgroundJobs/CompleteMeetingJob.cs`

- [ ] **Step 1: 实现 CompleteAsync 触发后台任务**

置 Status=Completed、EndedAt=now；提交 `CompleteMeetingJob`（ABP BackgroundJob）参数为 MeetingRecordId。

- [ ] **Step 2: 实现 CompleteMeetingJob**

仿 `AiAnalysisJob` 模式：`public class CompleteMeetingJob : AsyncBackgroundJob<CompleteMeetingArgs>, ITransientDependency`，参数类 `CompleteMeetingArgs { Guid MeetingRecordId }`。读取会议 TranscriptFile（会议中上传的全程录音）→ `_bot.TranscribeAsync` → 结果存转写文本文件 → 生成 Markdown 报告（出勤表 + 转写稿 + 问答记录）→ 存 `ReportFile`；更新会议记录。失败重试 2 次，仍失败记录日志并置错误状态。

- [ ] **Step 3: 运行测试并 Commit**

```bash
git add backend/DredgeAI.BidCompare
git commit -m "feat: MeetingBot 会后转写与报告"
```

### Task 3.9：AutoMapper 与 Swagger 验证

- [ ] **Step 1: 配置映射**

在 `BidCompareApplicationAutoMapperProfile` 注册：MeetingRecord→MeetingRecordDto、SpeechDraft→SpeechDraftDto、AttendanceRecord→AttendanceItemDto、QaRecord→QaRecordDto（Sources 由 SourcesJson 反序列化）。

- [ ] **Step 2: 启动后端并验证接口**

Run: `dotnet run --project src/DredgeAI.BidCompare.HttpApi.Host`
在 Swagger（https://localhost:44361/swagger）验证：创建会议、生成晨会稿、上传照片点名（需 meeting-bot 运行）、文本问答。

- [ ] **Step 3: Commit**

```bash
git add backend/DredgeAI.BidCompare
git commit -m "feat: MeetingBot AutoMapper 与接口验证"
```

### Task 3.10：前端切换真实 API 联调

- [ ] **Step 1: 关闭 meeting mock**

`user-web/src/utils/constants.ts` 中 `MOCK_MODULES.meeting = false`；确认 `API_BASE_URL` 指向后端地址。

- [ ] **Step 2: 端到端手工联调（电脑摄像头/麦克风）**

启动 meeting-bot（Task 1.11 脚本）与后端；浏览器走完 5 步：创建会议 → 生成晨会稿 → 拍照点名 → 文本/语音问答 → 完成并查看报告。

- [ ] **Step 3: 修复联调问题并 Commit**

```bash
git add user-web backend/DredgeAI.BidCompare
git commit -m "fix: AI晨会端到端联调修复"
```

### Task 3.11：端到端验收

- [ ] **Step 1: 按第 9 节时延目标实测**

记录 `/count`、`/recognize`、`/asr`、`/tts`、问答全链路时延，与设计文档第 9 节目标比对；超标的项记录原因（网络/模型/链路）并优化。

- [ ] **Step 2: 验证点名去重与补扫**

同一人两次入镜只算一次；未识别者出现在补扫列表。

- [ ] **Step 3: 输出验收报告**

写入 `docs/ai-meeting-acceptance.md`：环境、实测时延、准确率（点名/转写）、遗留问题清单。

- [ ] **Step 4: Commit**

```bash
git add docs/ai-meeting-acceptance.md
git commit -m "docs: AI晨会端到端验收报告"
```
