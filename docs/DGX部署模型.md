# AI 晨会模型部署与 API 需求清单

> 版本：v1.0（2026-08-25）
> 用途：提供给乙方（模型部署服务商）的部署与 API 交付要求

## 1. 背景说明

AI 晨会（meeting-bot）需要四类 AI 模型能力：**语音识别（ASR）、语音合成（TTS）、人脸识别、人数统计**。当前生产方案为“一模型一容器”架构，四个模型服务各自独立部署，由一个轻量聚合层（meeting-bot，端口 8101）统一对外提供 HTTP API。

> ⚠️ 注意：早期方案中的 FireRedASR-AED-L、FireRedTTS 已停用，**不要求乙方部署**，仅保留在代码历史中。

## 2. 模型清单

| 能力 | 模型 | 版本 / 仓库 | 权重文件 | 大小 | 推理设备 |
|---|---|---|---|---|---|
| 语音识别 ASR | SenseVoice-Small | ModelScope `iic/SenseVoiceSmall` | `model.pt`（funasr 加载） | ~893 MB | CPU 即可，可切 GPU |
| 语音合成 TTS | CosyVoice3 0.5B | ModelScope `FunAudioLLM/Fun-CosyVoice3-0.5B-2512` | `llm.pt` / `flow.pt` / `hift.pt` / `campplus.onnx` / `speech_tokenizer_v3.onnx` 等 | ~6 GB | GPU 必须（约 4–5 GB 显存） |
| 人脸识别 | InsightFace `buffalo_l` | deepinsight/insightface Releases v0.7 | `det_10g.onnx` / `w600k_r50.onnx` / `genderage.onnx` / `2d106det.onnx` / `1k3d68.onnx` | ~326 MB | CPU/GPU（onnxruntime） |
| 人数统计 | YOLOv8n | ultralytics assets v8.3.0 | `yolov8n.pt`（检测 `person` 类计数） | ~6 MB | CPU 即可 |

### 模型能力说明

- **ASR（SenseVoice-Small）**：支持中文/英文/日文/韩文/粤语，自动语种识别，输出带标点（ITN）。服务内部统一转 16k 单声道 WAV，超过 50 秒自动切片转写。
- **TTS（CosyVoice3-0.5B）**：输出 24kHz 单声道 WAV；默认音色 `zh-male-news`（男声新闻播报），支持多音色切换与自定义音色上传。
- **人脸识别（InsightFace buffalo_l）**：检测 + 识别一体；支持人脸注册（enroll）与人脸识别（recognize），人脸库以 `faces.json` 持久化落盘；识别阈值为 0.55（余弦相似度）。
- **人数统计（YOLOv8n）**：检测图片中 `person` 类别数量，返回总人数。

## 3. 部署与运行要求

1. **容器化部署**：四个模型各一个独立服务容器，权重文件挂载自宿主机目录（如 `D:/AI/AImodles/models`），不烧入镜像。
2. **硬件要求**：
   - TTS 必须 GPU 推理（约 4–5 GB 显存）；
   - ASR、人脸、人数可 CPU 推理；
   - 若目标机器为 DGX（ARM64），使用 `docker buildx --platform linux/arm64` 重建镜像，或改用 NVIDIA NIM。
3. **网络与下载源**（国内环境）：
   - 模型权重统一从 **ModelScope** 下载（HuggingFace/GitHub 直连慢或不可用）；
   - 系统包（apt）与 Python 依赖走阿里云镜像。
4. **健康检查**：每个服务必须提供 `/health`（TTS 为 `/api/health`），并返回 `model_loaded` 状态，供编排层依赖判断。
5. **鉴权**：所有接口均校验请求头 `X-Meeting-Bot-Key`，密钥由部署方与我方协商配置。

## 4. API 契约（乙方必须按此实现）

### 4.1 通用约定

- 鉴权头：`X-Meeting-Bot-Key: <key>`
- 数据格式：上传一律 multipart/form-data；TTS 返回二进制 WAV 流
- 服务端口（本地联调参考）：ASR `8102`、TTS `8000`、人脸 `8103`、人数 `8104`、聚合层 `8101`

### 4.2 接口明细

| 能力 | 方法 | 路径 | 入参 | 返回 |
|---|---|---|---|---|
| 健康检查 | GET | `/health`（TTS: `/api/health`） | – | `{"status":"ok","model_loaded":true,...}` |
| ASR | POST | `/asr` | multipart `audio` | `{"text":"转写文本"}` |
| TTS | POST | `/api/tts` | JSON `{"text","voice_id":"zh-male-news","speed":1.0}` | WAV 流（24kHz 单声道），响应头 `X-Duration-Sec` |
| TTS 音色列表 | GET | `/api/voices` | – | 音色列表（至少包含 `zh-male-news`） |
| TTS 音色试听 | GET | `/api/samples/{voice_id}.wav` | – | WAV 试听文件 |
| TTS 自定义音色 | POST | `/api/voices/upload` | multipart `file`,`name` | `{"voice_id","sample_url",...}` |
| 人脸识别 | POST | `/recognize` | multipart `image` | `{"faces":[{"workerId","name","confidence","bbox"}]}` |
| 人脸注册 | POST | `/enroll` | multipart `worker_id`,`name`,`image` | `{"ok":true}` |
| 人数统计 | POST | `/count` | multipart `image` | `{"count":N}` |

### 4.3 请求示例

```bash
# ASR
curl -X POST http://<host>:<port>/asr \
  -H "X-Meeting-Bot-Key: <key>" \
  -F "audio=@question.wav"

# TTS
curl -X POST http://<host>:<port>/api/tts \
  -H "X-Meeting-Bot-Key: <key>" \
  -H "Content-Type: application/json" \
  -d '{"text":"早上好，今日晨会开始","voice_id":"zh-male-news","speed":1.0}' \
  -o answer.wav

# 人脸识别
curl -X POST http://<host>:<port>/recognize \
  -H "X-Meeting-Bot-Key: <key>" \
  -F "image=@meeting.jpg"

# 人数统计
curl -X POST http://<host>:<port>/count \
  -H "X-Meeting-Bot-Key: <key>" \
  -F "image=@meeting.jpg"
```

## 5. 验收指标（我方实测基准）

| 能力 | 指标 |
|---|---|
| ASR | 短音频热调用秒级返回；中文转写带标点，无 `<\|zh\|>` 等标签残留；支持 >50s 长音频切片 |
| TTS | 24kHz WAV 可播放；热调用约 3.7s/短句；默认音色为男声新闻播报 |
| 人脸识别 | 同人 enroll→recognize 相似度约 1.0；异人约 0.05 判未识别（阈值 0.55）；CPU 单张 2–4s |
| 人数统计 | 双人合影返回 `count=2`；单张 0.1–0.5s |

## 6. 交付物要求

1. 按上述模型 ID/版本部署的四个模型服务，提供容器镜像与部署文档（含端口、环境变量、挂载目录）；
2. 提供四个服务的 API 文档（OpenAPI/Swagger 或等价物）；
3. 提供自测报告：每个接口的请求/响应样例、首次加载耗时与热调用耗时；
4. 如需替换为自研或商用模型，须先确认 API 契约与验收指标等价，并经我方联调验收。

## 7. 参考信息

- 聚合层部署文档：`docs/meeting-bot-deploy.md`
- 端到端验收报告：`docs/ai-meeting-acceptance.md`
- 模型服务源码：`services/sensevoice`、`services/cosyvoice`、`services/insightface`、`services/yolo`
- 编排文件：`services/meeting-bot/docker-compose.yml`
