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
| 拍照点名 | POST /api/meeting/records/{id}/attendance/recognize | ✅ 200（meeting-bot mock 返回空，真实人脸待模型就绪实测） |
| 文本问答 | POST /api/meeting/records/{id}/qa | ✅ 200，chitchat 实测返回正常；knowledge 走检索+LLM |
| 语音问答 | POST /api/meeting/records/{id}/qa/audio | ⏳ 待真实 ASR/TTS 就绪 |
| 完成 | POST /api/meeting/records/{id}/complete | ✅ 200，Completed + 后台任务入队 |
| 报告 | GET /api/meeting/records/{id}/report | ✅ 200，含出勤/问答记录 |

## 三、单元测试

`MeetingRecordAppServiceTests` 6 个用例全通过：
- Create → Draft + PreInfo 落库
- GenerateSpeech → 含前置信息 + 知识证据；检索失败降级
- Recognize → 阈值过滤 + 去重 + 未识别归集
- AskQa → 意图分类（knowledge/chitchat）
- Complete → 后台任务入队 + 报告可查

## 四、模型实测（待模型权重就绪后回填）

- ASR：60s 内短音频识别延迟、长音频切块转写正确性
- TTS：合成 WAV 可播放、参考音色自然度
- 人脸：enroll → recognize 同人置信度、异人误识率
- 人数：单/多人照片计数准确性

## 五、遗留问题

1. user-web `MOCK_MODULES.meeting` 切换真实 API 依赖 meeting-bot 真实模型就绪
2. 工人花名册导入（xlsx/zip）接口已实现，前端管理页尚未接入
3. 语音问答的 TTS 播放未接到前端播放器（useAudioPlayer 已存在，后续接入）
4. FireRedTTS 采用 main 分支（v1）；1S 保留为 DGX 升级路径（Windows 依赖 fairseq/pynini 不可用）
5. 全量后端测试套件因用户未提交的 CompareTask ClauseDrafts 重构（ClauseExtractionTests 编译错误）暂不能整体运行，与本模块无关
