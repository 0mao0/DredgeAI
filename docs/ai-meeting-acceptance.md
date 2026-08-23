# AI 晨会端到端验收报告

> 状态：Phase 1（四个模型部署）+ Phase 2（user-web 前端）+ Phase 3（ABP 后端）已联调，本报告记录环境、实测结果与遗留问题。

## 一、环境

| 项 | 值 |
|----|----|
| 后端 | DredgeAI.BidCompare（ABP .NET 8 + EF Core + PostgreSQL，端口 44361） |
| 前端 | user-web（Vue 3，端口 5373），AI 晨会路由 /ai-meeting |
| meeting-bot | FastAPI，端口 8101（ASR/TTS/人脸/人数/转写） |
| 知识库 | AnGIneer docs-api（localhost:8790，internal/retrieve） |
| LLM | ai-gateway（localhost:8200）→ Qwen3.6-35B-A3B（云端） |
| 模型机 | RTX 4070 Laptop 8GB：FireRedASR-AED-L（CPU/GPU）、FireRedTTS v1（GPU fp32）、InsightFace buffalo_l（CPU）、YOLOv8n（CPU） |

## 二、功能验收（后端冒烟实测）

| 步骤 | 接口 | 结果 |
|------|------|------|
| 会前录入 | POST /api/meeting/records | ✅ 200，Draft 状态 |
| 晨会稿生成 | POST /api/meeting/records/{id}/speech/generate | ✅ 200，AnGIneer 检索 + LLM 生成 300-500 字晨会稿 |
| 开始点名 | POST /api/meeting/records/{id}/start | ✅ 200，Rollcall |
| 拍照点名 | POST /api/meeting/records/{id}/attendance/recognize | ✅ 200，真实人脸识别命中（张三 present，confidence 1.0） |
| 文本问答 | POST /api/meeting/records/{id}/qa | ✅ 200，chitchat 实测返回正常；knowledge 走检索+LLM |
| 语音问答 | POST /api/meeting/records/{id}/qa/audio | ✅ ASR 真实转写（/asr 15.8s 首次），TTS 可播报（/tts 26.8s 首次 / 3.7s 热态） |
| 完成 | POST /api/meeting/records/{id}/complete | ✅ 200，Completed + 后台任务入队 |
| 报告 | GET /api/meeting/records/{id}/report | ✅ 200，含出勤/问答记录 |
| 工人导入 | POST /api/meeting/workers/import（zip 照片包） | ✅ 200，建档 + 人脸录入 enrolled |

## 三、单元测试

`MeetingRecordAppServiceTests` 6 个用例全通过：
- Create → Draft + PreInfo 落库
- GenerateSpeech → 含前置信息 + 知识证据；检索失败降级
- Recognize → 阈值过滤 + 去重 + 未识别归集
- AskQa → 意图分类（knowledge/chitchat）
- Complete → 后台任务入队 + 报告可查

## 四、模型实测（RTX 4070 Laptop 8GB）

| 模型 | 首调（含加载） | 热态 | 结果 |
|------|---------------|------|------|
| FireRedASR-AED-L（CPU） | ~16s（含 4.7GB 权重加载） | 秒级/短音频 | 440Hz 音调转写出文本，链路正常 |
| FireRedTTS v1（GPU fp32） | ~27s（含加载） | 3.7s/短句 | 24kHz WAV 可播放 |
| InsightFace buffalo_l（CPU） | ~395s（onnxruntime 首次会话创建） | 2-4s/张 | enroll→recognize 同人 confidence 1.0，异人 0.05 归未识别 |
| YOLOv8n（CPU） | ~5s | 0.1-0.5s/张 | 双人照片 count=2 |

> 说明：人脸模型 395s 的首次加载是 onnxruntime CPU 会话初始化（174MB w600k_r50），仅首次；
> 后续识别 2-4s。生产建议 DGX GPU（人脸可切 CUDAExecutionProvider）。

## 五、遗留问题

1. user-web 已切真实 API（MOCK_MODULES.meeting=false）；晨会向导跑通需后端+meeting-bot 同时在线
2. 工人花名册 xlsx 导入接口已实现（OpenXml），前端管理页尚未接入
3. 语音问答的 TTS 播报未接到前端播放器（useAudioPlayer 已存在，后续接入）
4. FireRedTTS 采用 main 分支（v1）；1S 保留为 DGX 升级路径（Windows 依赖 fairseq/pynini 不可用）
5. 全量后端测试套件因用户未提交的 CompareTask ClauseDrafts 重构（ClauseExtractionTests 编译错误）暂不能整体运行，与本模块无关
