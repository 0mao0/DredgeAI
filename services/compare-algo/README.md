# compare-algo 比标算法服务

**服务定位**：平台「确定性算法」基础设施，不涉及 LLM；所有大模型推理统一走 ai-gateway。

无状态确定性计算服务：消费 ABP 主服务转发的 AnGIneer 解析产物原文
（`doc_blocks_graph.jsonl` 节点 + `doc_blocks_graph_meta.json` 的 `{docMeta, outlines, pages}`，
本服务不直接对接 AnGIneer/MinerU），产出 `aiGenerated=false` 的 Evidence（similarity / pricing / metadata）。
产物字段语义见 `docs/superpowers/plans/dredgeai-consume-angineer-requirements.md`（v2）与计划「实测事实」节，
Evidence 契约见 `docs/superpowers/specs/2026-07-29-ai-bid-compare-design.md` §6.1。

## 环境

- Python 3.11+，包管理 uv

## 启动

```bash
uv sync
uv run uvicorn app.main:app --host 0.0.0.0 --port 8100
```

环境变量：

| 变量 | 默认 | 说明 |
|---|---|---|
| `COMPARE_ALGO_MAX_BODY_BYTES` | `52428800`（50MB） | 请求体大小上限，超限返回 413；非法取值（如 `50MB`）回退默认并记 warning |

部署注意：

- **内部服务，无任何认证**：仅监听私网地址 / 内网网段，由 ABP 主服务（compare-task）内部调用，禁止直接暴露公网。
- **内存估算**：请求体限流中间件为防御 parse-DoS 会完整缓冲请求体后再回放，峰值约 2× 请求体大小
  （按 50MB 上限约 100MB 瞬时）；请按此规划 uvicorn worker 数与容器内存限额。

## 测试

```bash
uv run pytest -q
```

## 接口

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | /healthz | 健康检查 |
| POST | /analyze/similarity | 两两查重 + 雷同簇 + OCR 降权 |
| POST | /analyze/pricing | 报价规律（等差/尾数/贴近度） |
| POST | /analyze/metadata | 元数据一致性 + 相同错别字 |

请求体：`{"taskId": "...", "documents": [{"docId", "blocks": [...], "meta": {...}}, ...]}`（2~5 份，
AnGIneer 产物字段名原样；`docId` 为 opaque 的 ABP 文档 id）。
响应：`{"evidences": [Evidence, ...]}`。

错误契约（`{"code", "message", "details"}` 信封）：

| 状态码 | code | 场景 |
|---|---|---|
| 422 | `IR_VALIDATION_FAILED` | 产物校验不合格，`details` 为 `[{"path", "message"}]` 字段级错误 |
| 413 | `REQUEST_TOO_LARGE` | 请求体超过大小上限 |
| 500 | `INTERNAL_ERROR` | 内部分析失败（含响应契约违规等服务端 bug），堆栈只进日志不进响应体 |

注意：

- 404 / 405 不走上述错误信封，使用 FastAPI 默认 `{"detail": "..."}` 响应（调用方按状态码处理即可）。
- 请求体大小与文档体量正相关（实测 1965 块文档约 2~3MB），5 份上限约 15MB，内部 HTTP 调用可接受。
- **多检测器可同时命中**：pricing 同一文档集可同时产出等差（`arithmetic`）+ 贴近度（`closeness`）+
  尾数（`tail`）多条证据；similarity 同一文档集可同时产出两两雷同与雷同簇（`metrics.cluster=true`）证据。
  消费方按 `type` + `docIds` / `metrics.pattern` / `metrics.cluster` 分组，不得假设一次调用至多一条证据。

### Evidence metrics 目录

消费方契约（相似度矩阵由 ABP 主服务基于两两证据的 `metrics.similarity` 组装，本服务不出矩阵接口）：

| 证据 | metrics 键 |
|---|---|
| similarity 两两雷同 | `similarity`（Dice 系数）、`avgBlockJaccard`、`matchedBlockCount`、`ocrSuspect` |
| similarity 雷同簇（≥3 份） | `cluster`（恒 `true`）、`memberCount`、`avgSimilarity`、`ocrSuspect` |
| pricing 等差 | `pattern`="arithmetic"、`commonDiff`、`maxDeviation`、`amounts` |
| pricing 尾数 | `pattern`="tail"、`tail`（整数部分末两位字符串）、`amounts` |
| pricing 贴近度 | `pattern`="closeness"、`spreadRatio`、`minAmount`、`maxAmount`、`amounts` |
| metadata 字段一致 | `field`（author / createdAt / creatorTool）、`value` |
| metadata 相同错别字 | `pattern`="shared-typo"、`sharedNgramCount`、`samples`（规范化文本，见已知限制） |

其中 `amounts` 为 `{docId: 投标总价}` 映射；`ocrSuspect=true` 表示证据已按 spec §4.5 降权并标注。

## 测试 fixture

- `tests/conftest.py`：3 份合成标书（raw 产物形态），覆盖低置信 OCR 降权、等差报价、相同错别字场景。
- `tests/fixtures/*.json`：4 份真实 AnGIneer 产物裁剪样本（来源：AnGIneer 知识库实测文档）：

  | 文件 | 源 docId | 块数 | 用途 |
  |---|---|---|---|
  | `haigang1.json` | `doc-12f45ca9` | 38 | 部分雷同对一端 |
  | `haigang2.json` | `doc-c8be9f8b` | 197 | 部分雷同对另一端 |
  | `pingshen_a.json` | `doc-020a5d97` | 10 | 评审办法副本对 A |
  | `pingshen_b.json` | `doc-1d0c4891` | 10 | 评审办法副本对 B |

  块数（38 / 197 / 10 / 10）为测试断言锚点，裁剪再生成后必须保持一致。
  裁剪方法见计划 `docs/superpowers/plans/2026-07-29-bid-compare-algo-service.md` Task 2
  （需要本机 AnGIneer 数据目录；字段裁剪清单以该 Task 为准）。
- fixture 中 `image_path` / `fileName` 等取值仅为**惰性标识符**：对应图片文件不随仓库提交，
  服务（含 pricing 表格解析）从不加载图片，只透传标识供前端按图定位。

## 已知限制

- **错别字检测的小语料边界**：低频 n-gram 碰撞以「全文仅出现一次」为可疑特征，原生文本极短
  （约 50 字以内）的文档几乎全字符低频，站点计数会虚高。真实标书不会这么小，合成测试数据除外。
- **错别字检测只看原生文本**：仅统计 `source == "text"` 的块；OCR 块中「错得一样」可能只是同一
  识别器对同一模板的相同误识别（真实数据 confidence 全 1.0，置信度无法区分），按设计排除。
- **shared-typo 证据的 `metrics.samples` 为规范化文本**（仅保留中日韩文字 / ASCII 字母数字，
  标点空白已剥离）；前端高亮需先对原文做同样规范化再定位子串。
