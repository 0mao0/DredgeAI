# AI 晨会模型服务「一模型一容器」改造设计

- 日期：2026-08-24
- 状态：用户已确认设计（拓扑 / 音色 / meeting-bot 角色）

## 1. 背景与目标

AI 晨会模块的模型服务 `services/meeting-bot` 目前是「四合一」单容器：ASR（FireRedASR-AED-L）、TTS（FireRedTTS-1S）、人脸识别（InsightFace buffalo_l）、人数统计（YOLOv8n），镜像约 30GB，模型权重挂载自 `D:\AI\AImodles\models`。

本次改造目标：

1. ASR 换成 **SenseVoice-Small**（体积约 900MB，性价比高）。
2. TTS 换成 **CosyVoice3-0.5B**（性价比高；`D:\AI\AImodles\cosyvoice` 已有本地服务、音色资产与权重）。
3. 四个模型「一模型一容器」，其他应用可直接调用任意模型服务。
4. 模型权重统一放 `D:\AI\AImodles`，服务全部跑 Docker。
5. 为迁移 DGX Spark（ARM64）铺路。

约束：

- ABP（.NET）与前端零改动：对外仍只有 meeting-bot `:8101` 一套 API。
- 不删 FireRed 旧权重（确认稳定后由人工清理）。
- `faces.json` 继续留在模型卷（模拟期方案；生产迁 PostgreSQL 为后续话题）。

## 2. 现状盘点

- `services/meeting-bot/app/routes/`：薄路由（asr / tts / face / count / transcribe），只收请求转给引擎。
- `services/meeting-bot/app/engines/`：真实模型逻辑（firered_asr / firered_tts + tts_worker / insightface_engine / yolo_engine / audio 工具）。
- 全局鉴权 `security.py`：校验 `X-Meeting-Bot-Key`（默认 dev-key）。
- 模型权重布局：`D:\AI\AImodles\models\{fireredasr-aed-l, fireredtts, buffalo_l, yolov8n.pt, faces.json}`。
- CosyVoice3 独立服务已存在于 `D:\AI\AImodles\cosyvoice`：FastAPI server.py（:8000）、`voices_config.json`（含多个克隆音色）、Python 3.10 venv、权重 `pretrained_models\Fun-CosyVoice3-0.5B`、CosyVoice 源码 clone（含 Matcha-TTS）。**尚未进 Docker，且无鉴权。**
- SenseVoice-Small 尚未下载。

## 3. 目标架构

一个 compose 编排五个容器，同一内部网络，服务名互访：

| 服务 | 端口 | 模型 | 职责 | 推理设备 |
|------|------|------|------|----------|
| sensevoice | 8102 | SenseVoice-Small | ASR：POST /asr（长音频内部切块） | 本机 CPU，DGX 可 GPU |
| cosyvoice | 8000 | CosyVoice3-0.5B | TTS：/api/tts、/api/voices、音色上传克隆 | GPU fp16 |
| insightface | 8103 | InsightFace buffalo_l | 人脸 enroll/recognize，持有 faces.json | CPU |
| yolo | 8104 | YOLOv8n | 人数统计 | CPU |
| meeting-bot | 8101 | —（无模型） | 聚合层：转发 + 长音频转写后台任务 | — |

鉴权：

- 所有服务（含 meeting-bot 与四个模型服务）统一复用现有 `MEETING_BOT_KEY`（默认 dev-key），校验 `X-Meeting-Bot-Key` 请求头；CosyVoice 的 server.py 需补鉴权依赖。
- meeting-bot 转发时带同一 key；其他应用带 key 可直接调任意模型服务。

## 4. 代码布局与组件

### 4.1 services/sensevoice（新建）

- FastAPI 服务，端口 8102，key 鉴权。
- `app/engines/sensevoice_asr.py`：funasr `AutoModel(model="iic/SenseVoiceSmall", model_dir=<MODEL_DIR>/SenseVoiceSmall)`；`ASR_DEVICE` 默认 cpu，可切 cuda。
- 音频工具从 meeting-bot 迁入：`to_wav_16k_mono`（ffmpeg）+ `split_wav_16k_mono`（>50s 切块）。
- 后处理：`AutoModel(..., disable_pbar=True)` 只取纯文本（不用时间戳/富输出），剥离 `<|zh|><|NEUTRAL|><|Speech|><|nospeech|>` 等标签；无语音返回空文本。
- `POST /asr`：multipart `audio` → `{text}`；`GET /health` 上报模型加载状态；推理端点用同步 `def`（FastAPI 自动进线程池），避免阻塞事件循环。
- 镜像基座：`nvidia/cuda:12.6.2-cudnn-runtime-ubuntu22.04`（本机 CPU、DGX 可切 GPU）。
- 依赖：fastapi / uvicorn / pydantic-settings / python-multipart / funasr / torch / torchaudio / numpy；apt 装 ffmpeg。

### 4.2 services/cosyvoice（新建，代码从 AImodles 复制）

- 把 `D:\AI\AImodles\cosyvoice\server.py`、`voices_config.json` 复制进仓库并改造：
  - 路径改为环境变量：`COSYVOICE_DATA`（资产根）、`MODEL_DIR`（权重）、`VOICES_CONFIG`。
  - `voices_config.json` 中相对 `wav` 与 `CosyVoice/asset`（含 samples）统一以 `COSYVOICE_DATA` 为根重映射，数据卷挂载保持原相对布局。
  - 补 `X-Meeting-Bot-Key` 鉴权依赖。
  - 默认音色 `zh-male-news`（男声·播报），`TTS_VOICE_ID` 可配置。
- 保留端点：`/api/tts`、`/api/voices`、`/api/samples/{id}.wav`、`/api/voices/upload`、`/api/health`。
- CosyVoice 源码放 `services/cosyvoice/third_party/CosyVoice`（gitignore，本地已有 clone），镜像构建时 COPY；依赖按本地 venv 已验证集合裁剪（torch 2.3.1 cu121、torchaudio、transformers、diffusers、librosa、soundfile、numpy、fastapi、uvicorn 等），不装 deepspeed / tensorrt；镜像基座 `nvidia/cuda:12.6.2-cudnn-runtime-ubuntu22.04`，apt 装 ffmpeg + libsndfile1（音色上传/试听转码与 loudnorm 依赖）。

### 4.3 services/insightface（新建，从 meeting-bot 迁移）

- 迁入 `insightface_engine.py`、face 接口（FaceEngine / FaceMatch / mock）、face 路由（`POST /recognize`、`POST /enroll`）。
- `faces.json` 位于 `<MODEL_DIR>/faces.json`（挂载 `D:\AI\AImodles\models`），注册数据不丢。
- 配置：`FACE_PROVIDERS=cpu`、`FACE_RECOGNIZE_THRESHOLD=0.55`、`MODEL_DIR=/app/models`。
- 依赖：insightface / onnxruntime / opencv-python / numpy / fastapi / uvicorn / pydantic-settings / python-multipart。

### 4.4 services/yolo（新建，从 meeting-bot 迁移）

- 迁入 `yolo_engine.py`、count 接口、count 路由（`POST /count`）。
- 权重 `yolov8n.pt` 位于 `<MODEL_DIR>`。
- 配置：`COUNT_DEVICE=cpu`、`MODEL_DIR=/app/models`。
- 依赖：ultralytics / torch / opencv-python / numpy / fastapi / uvicorn / pydantic-settings / python-multipart。

### 4.5 services/meeting-bot（改造为聚合层）

- 保留：`main.py`（路由 + 鉴权 + /health）、`settings.py`、`security.py`、`routes/transcribe.py`（后台任务）。
- `routes/asr.py` → HTTP 客户端调 `http://sensevoice:8102/asr`。
- `routes/tts.py` → HTTP 客户端调 `http://cosyvoice:8000/api/tts`（传 `text` + `voice_id=zh-male-news`）。
- `routes/face.py`、`routes/count.py` → multipart 转发到 insightface / yolo（httpx 重构造 files/data，保留 filename 与 Form 字段 worker_id/name）。
- `routes/transcribe.py` → 后台任务调 sensevoice `/asr`（sensevoice 内部处理长音频切块）。
- `settings.py` 新增服务 URL 配置：`SENSEVOICE_URL`、`COSYVOICE_URL`、`TTS_VOICE_ID`、`INSIGHTFACE_URL`、`YOLO_URL`；旧的引擎枚举（ASR_ENGINE 等）随引擎移除而废弃。
- 引擎代码从 meeting-bot 移除（firered_*、insightface_engine、yolo_engine 等），git 历史保留可回滚；insightface/yolo 引擎进入各自新服务。
- Dockerfile 瘦身为 python 3.12 slim + fastapi/uvicorn/httpx，不再装 torch/FireRed 依赖（镜像从约 30GB 缩到很小）。

## 5. 数据流

```
ABP / 其他应用 → meeting-bot:8101 ── HTTP（带 X-Meeting-Bot-Key）──
  ├─ /asr        → sensevoice:8102  → {text}
  ├─ /tts        → cosyvoice:8000   → WAV（voice_id=zh-male-news）
  ├─ /recognize  → insightface:8103 → {faces:[...]}
  ├─ /enroll     → insightface:8103 → {ok:true}
  ├─ /count      → yolo:8104        → {count}
  └─ /transcribe → 后台任务 → sensevoice:8102 → {job_id,status,text}
```

## 6. 错误处理

- 各模型服务启动时懒加载模型，`/health` 返回 `model_loaded` 与错误信息（cosyvoice 已有此模式，其余三个照做）。
- meeting-bot 使用 httpx AsyncClient 带超时（首加载 300s、常规 120s）；上游不可达或 5xx → 502/503 + 可读中文错误。
- 音频转换 / 切块失败 → 4xx 可读错误；无语音 → 空文本。
- CosyVoice 保持「模型在主线程加载/推理」的既有约束（server.py 已处理），不并发触碰模型对象。

## 7. 测试

单元测试（mock，不加载真实模型）：

- sensevoice：标签剥离、切块、/asr 接口。
- cosyvoice：鉴权依赖、/api/tts 请求参数（voice_id 默认值）。
- insightface / yolo：接口 + mock 引擎。
- meeting-bot：转发路由（mock httpx）、/transcribe 任务状态机。

容器冒烟：改造 `services/meeting-bot/tests/test_container_smoke.py` 为「先起五容器」，对五个端口 health + 经 meeting-bot 全 API 回归（asr / tts / recognize / enroll / count）。

人工验收：nvidia-smi 观察显存；各端口 curl；`faces.json` 持久化（容器内与宿主一致）。

## 8. 部署

新建 `scripts/deploy-model-services.ps1`：

0. 停掉旧裸跑进程（meeting-bot :8101 / CosyVoice :8000），释放端口与显存（8GB 卡避免 OOM）。
1. 下载 SenseVoice-Small：ModelScope `iic/SenseVoiceSmall` → `D:\AI\AImodles\models\SenseVoiceSmall`（约 900MB，funasr `AutoModel(model_dir=...)` 落盘）。
2. 检查其余权重：CosyVoice3（`D:\AI\AImodles\cosyvoice\pretrained_models\Fun-CosyVoice3-0.5B`）、buffalo_l、yolov8n.pt，缺失则下载。
3. `docker compose -f services/meeting-bot/docker-compose.yml up -d --build`。
4. 冒烟五容器。

既有 `scripts/deploy-meeting-bot.ps1` 保留（FireRed 回滚参考）。compose 沿用 `services/meeting-bot/docker-compose.yml`，扩展为五服务（模型权重与音色资产只挂载、不烧入镜像）：

- `D:/AI/AImodles/models` → sensevoice / insightface / yolo 的 `/app/models`。
- `D:/AI/AImodles/cosyvoice` → cosyvoice 容器的数据卷（权重 + 音色资产 + voices_config.json）。
- GPU：cosyvoice 固定启用；sensevoice/insightface/yolo 本机 CPU，DGX 上按需切 GPU。
- 每个新服务自带 `pyproject.toml` + `uv.lock` + `.dockerignore`；根 `.gitignore` 增加对 `services/{sensevoice,cosyvoice,insightface,yolo}/third_party`、`models`、`.venv` 的忽略。

## 9. DGX Spark 迁移

- 一模型一容器即迁移形态：DGX 上 `docker buildx --platform linux/arm64` 重建四个模型镜像。
- compose 网络 / 环境变量 / 挂载结构原样复用；设备开关（`ASR_DEVICE=cuda`、`FACE_PROVIDERS=gpu`、`COUNT_DEVICE=cuda`）。
- 风险与应对：funasr / onnxruntime / insightface / ultralytics 需 ARM64 wheel（torch 有官方 arm64）；CosyVoice 依赖已裁剪掉 deepspeed / tensorrt 以降低 ARM64 编译风险。

## 10. 决策记录

| # | 决策 | 依据 |
|---|------|------|
| D1 | 一模型一容器（sensevoice / cosyvoice / insightface / yolo） | 用户确认；其他应用可直接调用模型服务 |
| D2 | 默认音色 zh-male-news（男声·播报），环境变量可配置 | 用户确认 |
| D3 | meeting-bot 保留为聚合层，ABP 与前端零改动 | 用户确认（ABP 主要是页面侧工作） |
| D4 | SenseVoice 本机 CPU，DGX 可 GPU | 8GB 显存留给 CosyVoice；SenseVoice 小、CPU 足够快 |
| D5 | 统一复用 `MEETING_BOT_KEY` 鉴权（默认 dev-key） | 内部网络防误调用；CosyVoice 需补鉴权 |
| D6 | FireRed 代码保留于 git 历史，新镜像不装其依赖 | 镜像大幅瘦身；回滚 = 旧提交 / 旧镜像重建 |

## 11. 非目标

- 不改 ABP / 前端。
- 不做统一模型网关 / UI。
- 不删 FireRed 旧权重（人工确认后再说）。
- 不把 faces.json 迁到 PostgreSQL（后续单独设计）。
- 不动 ai-gateway / compare-algo。

## 12. 实施要点（计划阶段细化）

以下在 writing-plans 阶段定死，避免实施时踩坑：

- CosyVoice 数据卷挂载后，`voices_config.json` 的相对 wav 路径、`CosyVoice/asset`、`samples` 目录必须与 `COSYVOICE_DATA` 根一致；否则音色列表/试听/上传会 404 或写失败。
- meeting-bot 代理 multipart 时保留 `filename`、`content_type` 与 Form 字段（enroll 的 worker_id/name），并用 httpx 重构造请求。
- sensevoice 的 `/asr` 推理用同步端点（线程池），meeting-bot 的 `/transcribe` 后台任务需限并发/串行，避免长音频把 CPU 打满。
- SenseVoice 输出固定为纯文本：关富输出/时间戳，`use_itn` 与语言检测取值明确，标签剥离用正则统一。
- 各新服务 Dockerfile 用 `COPY pyproject.toml uv.lock` 保证可复现；`.dockerignore` 排除 venv/models/third_party。
- 迁移时把 meeting-bot 现有引擎相关单测（test_face / test_count / test_engines_integration 等）随代码迁到对应新服务。
