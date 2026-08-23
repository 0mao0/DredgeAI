# meeting-bot 真实模型部署记录

## 一、模型选型与权重来源

| 能力 | 模型 | 权重 | 下载源 | 大小 | 本机运行方式 |
|------|------|------|--------|------|--------------|
| ASR（转写/问答语音） | FireRedASR-AED-L | `pengzhendong/FireRedASR-AED-L` | ModelScope | ~4.7GB | 默认 CPU（fp32），可切 GPU |
| TTS（语音播报） | FireRedTTS（main 分支 v1，非 1S） | `FireRedTeam/FireRedTTS` | ModelScope | ~3.1GB | GPU fp32（约 3GB 显存） |
| 人脸识别 | InsightFace `buffalo_l` | deepinsight release v0.7 | GitHub Releases | ~330MB | CPU（onnxruntime） |
| 人数统计 | YOLOv8n | ultralytics assets v8.3.0 | GitHub Releases | 6.5MB | CPU |

> **为什么 TTS 用 main 分支而不是 1S**：FireRedTTS-1S 在 Windows 上无法直接部署——
> 1) 上游仓库包结构缺失 `__init__.py`，pip 安装后无法 import；
> 2) 语义 tokenizer 依赖 `fairseq`，其 C++ 扩展（libbleu）在 MSVC 下编译失败；
> 3) 依赖 `pynini`（WeTextProcessing），无 Windows wheel。
> main 分支（2024 版）无这些依赖、模型仅 3.1GB、fp32 即可跑在本机 8GB 显卡上。
> 1S 保留为 DGX（Linux）升级路径，仓库内已含其 Windows 适配补丁 `services/meeting-bot/patches/fireredtts-windows.patch`。

## 二、本机环境

- Windows + RTX 4070 Laptop 8GB（驱动 561 / CUDA 12.6），内存 33.7GB
- Python 3.12（meeting-bot 主环境，uv 管理）+ Python 3.10（FireRedTTS 专用环境 `.venv-tts`）
- 网络：github.com 主站不可直连，用 codeload/raw/Release 直链；HuggingFace 模型文件不可直连，模型统一走 ModelScope

## 三、一键部署

```powershell
.\scripts\deploy-meeting-bot.ps1          # 依赖 + 模型 + 启动（默认 8101）
.\scripts\deploy-meeting-bot.ps1 -SkipStart
```

脚本做的事：
1. `uv sync --group models`（torch 2.9.1+cu126、fireredasr、insightface、onnxruntime、ultralytics、opencv、huggingface_hub）
2. 创建 Python 3.10 venv 并安装 torch 2.3.1+cu121 + FireRedTTS 依赖（跳过 pynini，已做免依赖降级）
3. 下载 FireRedTTS 源码（codeload tarball）→ 打补丁（`__init__.py`、设备硬编码、pynini 降级）→ `pip install -e`
4. 下载 YOLOv8n、buffalo_l（GitHub 直链）
5. 下载 FireRedASR-AED-L（ModelScope 直链，~4.7GB）→ `models/fireredasr-aed-l/`
6. 下载 FireRedTTS 权重（ModelScope 直链，~3.1GB）→ `models/fireredtts/pretrained_models/`
7. 启动 `uvicorn app.main:app --port 8101`

## 四、引擎实现

`services/meeting-bot/app/engines/`：
- `firered_asr.py`：本地权重加载，音频统一转 16k 单声道 WAV（依赖 ffmpeg），>50s 自动切块（AED 上限 60s）
- `firered_tts.py` + `tts_worker.py`：常驻子进程（3.10 venv），stdin/stdout JSON 协议，返回 24k WAV；默认内置 `examples/prompt_1.wav` 参考音色
- `insightface_engine.py`：buffalo_l 检测+识别，embedding 余弦匹配，人脸库落盘 `models/faces.json`；识别阈值 `FACE_RECOGNIZE_THRESHOLD`（默认 0.55）
- `yolo_engine.py`：YOLOv8n 统计 `cls==0`（person）框数

引擎切换通过环境变量（`.env.example`）：

```dotenv
ASR_ENGINE=firered       # mock | firered
TTS_ENGINE=firered       # mock | firered
FACE_ENGINE=insightface  # mock | insightface
COUNT_ENGINE=yolo        # mock | yolo
ASR_DEVICE=auto          # auto | cuda | cpu（8GB 显存建议 CPU）
TTS_DEVICE=auto          # auto | cuda | cpu（建议 cuda + fp16 由 worker 自动开）
FACE_PROVIDERS=cpu       # cpu | gpu
```

## 五、API 清单（均需 `X-Meeting-Bot-Key` 请求头）

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/health` | 健康检查 |
| POST | `/asr` | multipart `audio` → `{text}` |
| POST | `/tts` | JSON `{text}` → WAV 流 |
| POST | `/recognize` | multipart `image` → `{faces:[{worker_id,name,confidence,bbox}]}` |
| POST | `/enroll` | multipart `worker_id,name,image` → `{ok:true}` |
| POST | `/count` | multipart `image` → `{count}` |
| POST | `/transcribe` | multipart `audio` → `{job_id}`（长音频后台转写） |
| GET | `/transcribe/{job_id}` | 轮询 `{status,text}` |

## 六、冒烟

```powershell
$env:MEETING_BOT_INTEGRATION=1
uv run pytest tests/test_engines_integration.py -v   # 需要 data/meeting-bot/samples/meeting.jpg（含人照片）做视觉断言
```

## 七、DGX 迁移提示

- ASR/TTS 换用 GPU 推理：`ASR_DEVICE=cuda`、`TTS_DEVICE=cuda`（1S 如需升级，按 `patches/fireredtts-windows.patch` 反向应用到 Linux 源码，去掉 pynini/fairseq 问题后 1S 可跑）
- 模型目录与 venv 不随仓库提交（gitignore 已配置），DGX 上重复执行部署脚本即可
