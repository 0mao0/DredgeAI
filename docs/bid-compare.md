# 比标模块（AI 投标-比标）

> 本文是比标模块的功能细节；总体介绍与部署见根目录 [README](../README.md)。

## 定位

在 AI 投标工作区中，比标（BidCompare）对多份投标文件与可选招标文件进行结构化对比，产出相似度、报价规律与元数据一致性证据，支持 AI 条款判定与报告导出。它与读标（招标文件解读）是当前已打通真实后端链路的子应用。

## 端到端流程

1. 创建比标任务
2. 上传投标文件（`role=bid`）与可选招标文件（`role=tender`）；上传前本地校验文件头，扩展名与内容不符时仅提示、不拦截（解析链路按内容识别格式）
3. AnGIneer docs-api 解析文档，产出 `doc_blocks_graph.jsonl` + meta
4. 后端映射 / 校验为内部 IR（`ir.json`）
5. compare-algo 产出证据：
   - `similarity`：文档相似度
   - `pricing`：报价规律
   - `metadata`：元数据一致性
6. （可选）条款提取 / 确认，并经 ai-gateway 调用 LLM 做条款响应判定与关键指标比选
7. 结果工作台：证据清单、相似度热力图、PDF 对照阅读
8. 导出 docx / pdf 报告

## 状态与容错

- 文档解析：Pending → Parsing → Succeeded / Failed；长期停滞或超时由看门狗（`StuckTaskWatchdogWorker`）自动标记
- 任务：Parsing → Comparing → AiAnalyzing → Done / Failed（部分文档失败时进入 partial）
- 解析任务由后台队列串行执行；AnGIneer 轮询带停滞检测（progress/stage 指纹无变化 → resume 一次 → 仍无变化判失败），停滞与总超时阈值可配置（默认 20 分钟停滞 / 60 分钟总超时）
- 普通解析失败后重新解析会重新上传并创建新的解析任务，不复用死记录

## 已知限制

- AnGIneer v1 产物接口目前只开放 graph/meta，`content.md` 与图片待其开放后自动随包下载
- LLM 未配置（`LLM_CONFIGS` 为空）或 ai-gateway 未启动时，AI 分析自动降级为「暂不可用」，算法证据不受影响

## 相关实现位置

| 关注点 | 位置 |
|---|---|
| 后端业务逻辑 | `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.Application/`（CompareTasks、BackgroundJobs、Drafts、Evidences、ClauseTemplates、Reporting） |
| AnGIneer 客户端 | `backend/DredgeAI.BidCompare/src/DredgeAI.BidCompare.HttpApi.Host/AnGineer/` |
| 算法服务 | `services/compare-algo/` |
| 前端页面 | `user-web/src/views/ai-bid/compare/` |
| 接口约定 | `docs/backend-ABP接口响应格式标准.md` |
