# 比标算法服务 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **修订：2026-08-08（v2 契约修订）**：算法服务**直接消费 AnGIneer 解析产物**（`doc_blocks_graph.jsonl` + `doc_blocks_graph_meta.json` 内容随请求体传入），不再要求 ABP 预映射为内部适配 IR。服务内新增 `app/angineer/` 适配层承担全部产物 schema 知识。本修订基于对 AnGIneer 源码与 13 份真实 `parsed/` 样本的实测（见「实测事实」节）。

**Goal:** 新建独立 Python 微服务 `compare-algo`，消费 ABP 主服务转发的 **AnGIneer 解析产物原文**（jsonl 节点数组 + meta 内容），产出结构化确定性证据项（Evidence），覆盖 P1 全部能力：产物校验与适配（类型映射/HTML 标签剥离/PDF 日期归一）、两两查重（shingling + MinHash/LSH 粗筛 + 块级对齐）、雷同簇聚类、OCR 降权、报价规律分析（等差/尾数/贴近度）、元数据与相同错别字比对、FastAPI 三个分析接口与统一错误处理。

**Architecture:** 无状态计算服务，由 ABP 主服务（compare-task）通过 HTTP 调用。请求体为任务内 2~5 份文档的 AnGIneer 产物 JSON（**AnGIneer 字段名原样**，snake_case），响应为 Evidence 列表（camelCase，逐字遵守 spec §6.1）。服务内部分层：`angineer/`（`raw.py` 原始产物 pydantic 校验 + `adapter.py` 适配为内部分析模型 + `pdf_date.py` PDF 日期归一）→ `schemas/`（`ir.py` 内部分析模型 + `evidence.py` + `api.py`）→ `similarity/`、`pricing/`、`metadata/`（三个确定性分析域，各自纯函数 + service 组装证据，只消费内部模型）→ `main.py`（FastAPI 接口与异常处理器）。本服务只产出 `aiGenerated=false` 的 `similarity|pricing|metadata` 三类证据；`clause|indicator` 属 compare-ai 语义服务，不在此实现。

**Tech Stack:** Python 3.11 + FastAPI + pydantic v2 + datasketch（MinHash/LSH）+ beautifulsoup4/lxml（表格 html）+ pytest + httpx（TestClient）+ uv（包管理，pyproject.toml 单一事实源）

**假设（已在设计中拍板）:**

- 查重算法：段落级 n-gram shingling（中文按字 trigram 为主）→ datasketch MinHash/LSH 粗筛候选对 → difflib 块级精确对齐 → 输出相似度与对齐块列表；雷同簇聚类（≥3 份共同雷同单独标记）。
- 报价分析：从表格 html 中解析数值（正则 + 千分位/万元单位归一），检测等差、尾数规律、贴近度。
- 元数据比对：author/creatorTool/createdAt 一致性 + 相同错别字检测（低频错字 n-gram 碰撞）。
- OCR 降权：`source=ocr` 且 `confidence<0.5` 的块单独标注，不作为强证据（spec §4.5）；`source/confidence` 为 null 的块不参与降权（v2 §4 降级期关闭，不误伤）。
- 表格 html 解析用 beautifulsoup4 / lxml。
- 测试：pytest，TDD（每个任务先写失败测试再实现）。
- 包管理用 **uv**（`uv sync` / `uv run pytest`），全计划统一，不混用 pip。
- **请求契约（v2 修订）**：`{taskId, documents: [{docId, blocks, meta}]}`；`blocks` 为 `doc_blocks_graph.jsonl` 逐行节点（字段名原样），`meta` 为 `doc_blocks_graph_meta.json` 的 `{docMeta, outlines, pages}` 子集（`edges/stats` 等忽略）。ABP 主服务从 S3 读产物原文转发，零 C# 映射逻辑。`docId` 为 ABP 文档 id（opaque 字符串，**不要求** block_uid 以其为前缀）。
- 服务落在本 monorepo 的 `services/compare-algo/`（与 ABP 主服务解耦部署，仅 HTTP 契约耦合）。
- 唯一事实源：`docs/superpowers/specs/2026-07-29-ai-bid-compare-design.md`（下称 spec）的 §6.1（Evidence 字段名逐字遵守）；产物字段语义以 `docs/superpowers/plans/dredgeai-consume-angineer-requirements.md`（2026-08-07 v2，下称 v2 文档）为准，并以本计划「实测事实」节为最终裁决。

**实测事实（2026-08-08 对 AnGIneer 源码 + 13 份真实 parsed/ 样本核验，优先级高于 v2 文档措辞）:**

1. **`source` 词表为 `text` / `ocr` / `table` / `formula` / null**（`solo_engine.py:1225-1257`）：原生文本写作 **`text`**（v2 文档 §四的 `native` 系措辞，以实测为准）；image/chart 块为 null。当前数据中 confidence 全为 1.0（真实 OCR 分数流存在但 hybrid 引擎给 1.0 或缺失→null）。
2. **`chart` 是真实 block_type**（实测 2 例：有 `image_path`、`plain_text` 为空、source=null）——v2 §3 映射表遗漏，适配层按 `image` 处理。
3. **`docMeta.createdAt` 是 PDF 原始日期**（如 `D:20251229164720+08'00'`），不是 ISO——适配层归一为 ISO 8601（无法解析/已是 ISO 则原样保留，相等性比对不受影响）。
4. **标题 `plain_text` 可能含 HTML 标签**（实测 `"2<sub>.</sub> 1 一般规定"`）——适配层统一剥标签。
5. **bbox 全部 0~1 归一化**（实测 2544 块无 null、无越界）；`pages` 给真实 pt 尺寸（如 612.0×825.0，**浮点**）；块级 `page_width/page_height=1000.0` 是 MinerU 归一化坐标系标记，不使用。
6. **table_html 全部纯净**（132 表：仅 `table/tr/td` + `rowspan/colspan`，无 th/thead/class/style）；**但 2/132 表格无 `table_html`**（有整表截图）——内部模型放宽为 table 必须有 `imgPath`、html 可选，pricing 跳过无 html 表格。
7. **真实 fixture 素材**：`doc-12f45ca9`（海港1，38 块）vs `doc-c8be9f8b`（海港2，197 块）为部分雷同对（实测按计划算法 Dice≈0.346 → low 证据）；`doc-020a5d97`/`doc-1d0c4891`（评审办法副本，各 10 块）author 相同、creatorTool 均为 Writer、createdAt 不同（→ author mid + creatorTool low 两条元数据证据，无 createdAt 证据）；两对均经本计划任务断言固化。
8. AnGIneer HTTP 产物下载白名单不含 content.md/images（仅 jsonl/meta.json/sqlite）；本服务不需要 content.md（有损投影，面向 LLM 语义层）。

---

## 决策摘要

| 决策项 | 选择 |
|---|---|
| 请求契约 | ABP 转发 AnGIneer 产物原文（`{docId, blocks, meta}`），服务内 `angineer/` 适配层映射为内部模型 |
| 包管理 | uv（pyproject.toml + `[dependency-groups]` dev） |
| 服务位置 | `services/compare-algo/`（新目录，不动现有 user-web/admin-web） |
| 相似度阈值 | LSH 粗筛 0.5；精确 Jaccard 复核 ≥0.5；出证据 Dice ≥0.3；severity high ≥0.8 / mid ≥0.5 / low 其余 |
| 雷同簇 | Union-Find 归并两两相似度 ≥0.5 的文档，簇 ≥3 份单独出 high 证据 |
| OCR 降权 | 命中低置信 OCR 块的证据 severity 降一级 + `metrics.ocrSuspect=true` + 文案标注；`source/confidence` 为 null 的块不参与降权（v2 §4 降级期关闭） |
| 测试 fixture | 真实样本（海港对 + 评审办法副本对，tests/fixtures/）+ 合成 raw 样本（低置信 OCR/等差报价/相同错别字场景） |
| 提交 | 每个 Task 一次 git commit（conventional commits） |

## 目标文件结构

```
services/compare-algo/
├── pyproject.toml
├── README.md
├── app/
│   ├── __init__.py
│   ├── main.py                  # FastAPI 入口：三接口 + 统一异常处理
│   ├── ocr.py                   # OCR 低置信判定与 severity 降级（spec §4.5）
│   ├── angineer/                # AnGIneer 产物适配层（唯一理解产物 schema 的地方）
│   │   ├── __init__.py
│   │   ├── raw.py               # 原始产物 pydantic 模型 + 宽松校验（jsonl 节点 / meta）
│   │   ├── adapter.py           # 产物 → 内部分析模型（类型映射/剥标签/日期归一/outline 嵌套化）
│   │   └── pdf_date.py          # PDF 原始日期 D:YYYY... 解析归一为 ISO 8601
│   ├── schemas/
│   │   ├── __init__.py
│   │   ├── ir.py                # 内部分析模型（camelCase IrDocument，分析引擎消费）
│   │   ├── evidence.py          # Evidence 模型 + build_evidence 组装器
│   │   └── api.py               # AnalyzeRequest/AnalyzeResponse/ErrorResponse
│   ├── similarity/
│   │   ├── __init__.py
│   │   ├── shingle.py           # 文本规范化 + 字级 n-gram + jaccard
│   │   ├── minhash.py           # MinHash/LSH 粗筛候选块对
│   │   ├── align.py             # difflib 块级单调对齐 + Dice 相似度
│   │   ├── cluster.py           # Union-Find 雷同簇聚类
│   │   └── service.py           # analyze_similarity 证据组装
│   ├── pricing/
│   │   ├── __init__.py
│   │   ├── number_norm.py       # 金额解析（千分位/万元归一）
│   │   ├── table_parse.py       # 表格 html 展开为网格 + 总价提取
│   │   ├── patterns.py          # 等差/尾数/贴近度检测
│   │   └── service.py           # analyze_pricing 证据组装
│   └── metadata/
│       ├── __init__.py
│       └── service.py           # 元数据比对 + 相同错别字 n-gram 碰撞
└── tests/
    ├── __init__.py
    ├── conftest.py              # 合成 raw fixture（3 份虚拟标书）+ 真实 fixture 加载器
    ├── fixtures/                # 裁剪后的真实 AnGIneer 产物（Task 2 生成）
    │   ├── haigang1.json        # doc-12f45ca9（38 块，部分雷同一端）
    │   ├── haigang2.json        # doc-c8be9f8b（197 块，部分雷同另一端）
    │   ├── pingshen_a.json      # doc-020a5d97（10 块，副本对 A）
    │   └── pingshen_b.json      # doc-1d0c4891（10 块，副本对 B）
    ├── test_smoke.py
    ├── test_angineer_raw.py
    ├── test_angineer_adapter.py
    ├── test_evidence.py
    ├── test_ocr.py
    ├── test_shingle.py
    ├── test_minhash.py
    ├── test_align.py
    ├── test_cluster.py
    ├── test_similarity_service.py
    ├── test_pricing_parse.py
    ├── test_pricing_patterns.py
    ├── test_pricing_service.py
    ├── test_metadata.py
    └── test_api.py
```

## 通用约定（每个 Task 都遵守）

- TDD 节奏：写失败测试 → `uv run pytest <测试文件> -q` 确认失败 → 最小实现 → 再跑确认通过 → `git add services/compare-algo && git commit`。
- 所有命令的工作目录为仓库根目录 `D:/AI/DredgeAI`，pytest 命令前缀 `cd services/compare-algo &&`。
- 模块内 import 一律用绝对导入（`from app.xxx import ...`），pytest 通过 pyproject 的 `pythonpath = ["."]` 解析。
- Evidence 一律通过 `build_evidence(...)` 组装，禁止手工拼 dict。
- 证据 title/description 中面向用户的文档标识一律使用 `meta.fileName`（缺失时回退 docId），禁止直接暴露 opaque docId；Tasks 13–15 的 pricing/metadata service 同样遵守，若其代码块仍用 docId 以本约定为准。
- **全部 AnGIneer 产物字段名/取值知识只允许出现在 `app/angineer/` 包内**；下游分析引擎只消费 `app/schemas/ir.py` 的 camelCase 内部模型，禁止直接读取 snake_case 产物字段。
- 请求/产物层用 AnGIneer 字段名原样（snake_case）；内部模型与 Evidence 响应用 camelCase（Evidence 逐字遵守 spec §6.1）。

---

## Task 1: 项目脚手架（uv + pyproject + 目录骨架 + pytest 冒烟）

**Files:**
- Create: `services/compare-algo/pyproject.toml`
- Create: `services/compare-algo/app/__init__.py`（空文件）
- Create: `services/compare-algo/app/angineer/__init__.py`（空文件）
- Create: `services/compare-algo/app/schemas/__init__.py`（空文件）
- Create: `services/compare-algo/app/similarity/__init__.py`（空文件）
- Create: `services/compare-algo/app/pricing/__init__.py`（空文件）
- Create: `services/compare-algo/app/metadata/__init__.py`（空文件）
- Create: `services/compare-algo/tests/__init__.py`（空文件）
- Create: `services/compare-algo/tests/test_smoke.py`

- [ ] **Step 1: 确认 uv 可用（前置条件）**

```bash
uv --version
```

若未安装，按 https://docs.astral.sh/uv/ 安装后继续；本计划不覆盖 uv 的安装。

- [ ] **Step 2: 写 pyproject.toml**

`services/compare-algo/pyproject.toml`：

```toml
[project]
name = "compare-algo"
version = "0.1.0"
description = "比标算法服务：消费 AnGIneer 解析产物（ABP 转发），产出确定性 Evidence"
requires-python = ">=3.11"
dependencies = [
    "fastapi>=0.115",
    "uvicorn>=0.30",
    "pydantic>=2.7",
    "datasketch>=1.6",
    "beautifulsoup4>=4.12",
    "lxml>=5.2",
]

[dependency-groups]
dev = [
    "pytest>=8.0",
    "httpx>=0.27",
]

[tool.pytest.ini_options]
pythonpath = ["."]
testpaths = ["tests"]
```

- [ ] **Step 3: 创建包目录与空 `__init__.py`**

```bash
mkdir -p services/compare-algo/app/angineer services/compare-algo/app/schemas services/compare-algo/app/similarity services/compare-algo/app/pricing services/compare-algo/app/metadata services/compare-algo/tests/fixtures
touch services/compare-algo/app/__init__.py services/compare-algo/app/angineer/__init__.py services/compare-algo/app/schemas/__init__.py services/compare-algo/app/similarity/__init__.py services/compare-algo/app/pricing/__init__.py services/compare-algo/app/metadata/__init__.py services/compare-algo/tests/__init__.py
```

- [ ] **Step 4: 写冒烟测试**

`services/compare-algo/tests/test_smoke.py`：

```python
def test_smoke() -> None:
    assert True
```

- [ ] **Step 5: 安装依赖并跑冒烟测试，确认通过**

```bash
cd services/compare-algo && uv sync && uv run pytest -q
```

预期：`1 passed`。

- [ ] **Step 6: 提交**

```bash
git add services/compare-algo && git commit -m "chore(compare-algo): 项目脚手架（uv + pyproject + pytest 冒烟）"
```

---

## Task 2: 真实产物 fixture 准备（裁剪脚本 + tests/fixtures/）

**Files:**
- Create: `services/compare-algo/tests/fixtures/haigang1.json`
- Create: `services/compare-algo/tests/fixtures/haigang2.json`
- Create: `services/compare-algo/tests/fixtures/pingshen_a.json`
- Create: `services/compare-algo/tests/fixtures/pingshen_b.json`

**前置条件**：本机存在 AnGIneer 仓库数据目录 `D:/AI/AnGIneer/data/knowledge_base/libraries/default/documents/`（含 4 份样本的 `parsed/`）。若样本被清理，按 README（Task 17）重新解析或换样本后同步更新 Task 4/16 的断言值。

**裁剪规则**：只保留适配层读取的节点字段（`content_json`、`caption_*`、`markdown_line_*` 等一律剔除，减重且防误用）；meta 只保留 `docMeta`/`outlines`/`pages`。产物形态 = 请求体 documents 元素形态（`{docId, blocks, meta}`），conftest 加载后可直接 POST（Task 16）。

- [ ] **Step 1: 运行裁剪命令生成 4 份 fixture**

```bash
python -c "
import json, pathlib
SRC = pathlib.Path(r'D:/AI/AnGIneer/data/knowledge_base/libraries/default/documents')
DST = pathlib.Path('services/compare-algo/tests/fixtures')
KEEP = ['block_uid','block_type','page_idx','block_seq','plain_text','bbox','derived_level','parent_uid','source','confidence','image_path','table_html','math_content','formula_body','formula_number']
DOCS = {'haigang1':'doc-12f45ca9','haigang2':'doc-c8be9f8b','pingshen_a':'doc-020a5d97','pingshen_b':'doc-1d0c4891'}
DST.mkdir(parents=True, exist_ok=True)
for name, doc in DOCS.items():
    parsed = SRC / doc / 'parsed'
    blocks = [{k: b.get(k) for k in KEEP} for b in map(json.loads, open(parsed/'doc_blocks_graph.jsonl', encoding='utf-8'))]
    meta = json.load(open(parsed/'doc_blocks_graph_meta.json', encoding='utf-8'))
    payload = {'docId': doc, 'blocks': blocks, 'meta': {'docMeta': meta['docMeta'], 'outlines': meta['outlines'], 'pages': meta['pages']}}
    (DST / f'{name}.json').write_text(json.dumps(payload, ensure_ascii=False, indent=1), encoding='utf-8')
    print(name, len(blocks), 'blocks')
"
```

预期输出：

```
haigang1 38 blocks
haigang2 197 blocks
pingshen_a 10 blocks
pingshen_b 10 blocks
```

- [ ] **Step 2: 抽样核对 fixture 内容（固化断言值）**

```bash
python -c "
import json
base = pathlib = __import__('pathlib').Path('services/compare-algo/tests/fixtures')
h1 = json.loads((base/'haigang1.json').read_text(encoding='utf-8'))
print('h1 meta:', json.dumps(h1['meta']['docMeta'], ensure_ascii=False))
print('h1 outlines:', len(h1['meta']['outlines']), '| pages:', h1['meta']['pages'])
print('h1 types:', sorted({b['block_type'] for b in h1['blocks']}))
pa = json.loads((base/'pingshen_a.json').read_text(encoding='utf-8'))
pb = json.loads((base/'pingshen_b.json').read_text(encoding='utf-8'))
print('pingshen author 相同:', pa['meta']['docMeta']['author'] == pb['meta']['docMeta']['author'], pa['meta']['docMeta']['author'])
print('pingshen tool:', pa['meta']['docMeta']['creatorTool'], '| createdAt 不同:', pa['meta']['docMeta']['createdAt'] != pb['meta']['docMeta']['createdAt'])
"
```

预期（实测值，Task 4/16 断言以此为据）：
- haigang1：`creatorTool="Adobe Acrobat 9.3.2"`，`createdAt="D:20251229164720+08'00'"`（PDF 原始日期），outlines 4 条（1 根 + 3 子），pages 2 页 612.0×825.0，block_type ∈ {title, paragraph, page_header, page_number, equation_interline}
- pingshen 对：author 相同（非空），creatorTool 均为 `Writer`，createdAt 不同

- [ ] **Step 3: 提交**

```bash
git add services/compare-algo && git commit -m "test(compare-algo): 真实 AnGIneer 产物 fixture（海港部分雷同对 + 评审办法副本对）"
```

## Task 3: AnGIneer 原始产物 schema（raw.py 宽松校验）

**Files:**
- Create: `services/compare-algo/app/angineer/raw.py`
- Create: `services/compare-algo/tests/test_angineer_raw.py`

校验原则：**宽松但守底线**——只校验适配层读取的字段，未知字段一律忽略（`extra="ignore"`，产物后续增补字段不破坏契约）；`docId` 为 opaque 字符串（ABP 文档 id），**不校验** block_uid 前缀一致性。校验失败抛 `pydantic.ValidationError`（含字段路径），Task 16 统一转 422 `IR_VALIDATION_FAILED`。

硬性校验项：每文档 `blocks` 非空且 `block_uid` 非空唯一；`block_type` 非空字符串；`page_idx >= 0`；`bbox` 允许 null，存在时必须为 0~1 归一化（非负、x1≥x0、y1≥y0、≤1，像素坐标拒收）；`source` 只允许 `text|ocr|table|formula|null`（实测词表，`"native"` 等未知值拒收）；`confidence∈[0,1]` 或 null；`table_html` 存在时必须纯净（仅 table/tr/td/th、仅 rowspan/colspan 属性——实测 132 表全满足）；`meta.docMeta` 六字段（fileName/pageCount/author/creatorTool/createdAt/modifiedAt）可 null 不可省略；`meta.pages` 至少 1 页且 width/height 为正浮点；`meta.outlines` 可为空列表。

- [ ] **Step 1: 写失败测试**

`services/compare-algo/tests/test_angineer_raw.py`：

```python
import json
import pathlib

import pytest
from pydantic import ValidationError

from app.angineer.raw import validate_raw_document

FIXTURES = pathlib.Path(__file__).parent / "fixtures"


def load_fixture(name: str) -> dict:
    return json.loads((FIXTURES / f"{name}.json").read_text(encoding="utf-8"))


def _minimal_raw() -> dict:
    return {
        "docId": "d1",
        "blocks": [
            {
                "block_uid": "d1:0:1",
                "block_type": "paragraph",
                "page_idx": 0,
                "block_seq": 1,
                "plain_text": "正文内容",
                "bbox": [0.04, 0.06, 0.5, 0.1],
                "derived_level": None,
                "parent_uid": None,
                "source": "text",
                "confidence": 1.0,
                "image_path": None,
                "table_html": None,
                "math_content": None,
                "formula_body": None,
                "formula_number": None,
            }
        ],
        "meta": {
            "docMeta": {
                "fileName": "a.pdf",
                "pageCount": 1,
                "author": None,
                "creatorTool": None,
                "createdAt": None,
                "modifiedAt": None,
            },
            "outlines": [],
            "pages": [{"pageIdx": 0, "width": 612.0, "height": 825.0}],
        },
    }


class TestRealFixtures:
    """真实产物必须通过校验（Task 2 裁剪样本）。"""

    @pytest.mark.parametrize("name", ["haigang1", "haigang2", "pingshen_a", "pingshen_b"])
    def test_real_fixture_valid(self, name):
        doc = validate_raw_document(load_fixture(name))
        assert doc.docId.startswith("doc-")
        assert len(doc.blocks) > 0


def test_minimal_raw_passes():
    doc = validate_raw_document(_minimal_raw())
    assert doc.blocks[0].block_uid == "d1:0:1"


def test_unknown_extra_fields_ignored():
    # 产物后续增补字段不破坏契约（实测节点有 content_json/caption_* 等 40+ 字段）
    data = _minimal_raw()
    data["blocks"][0]["content_json"] = {"paragraph_content": []}
    data["blocks"][0]["some_future_field"] = 123
    data["meta"]["edges"] = []
    doc = validate_raw_document(data)
    assert doc.blocks[0].plain_text == "正文内容"


def test_docid_is_opaque_no_prefix_check():
    # docId 为 ABP 文档 id，不要求 block_uid 以其为前缀
    data = _minimal_raw()
    data["docId"] = "abp-guid-0001"
    doc = validate_raw_document(data)
    assert doc.docId == "abp-guid-0001"


def test_missing_block_uid_rejected():
    data = _minimal_raw()
    del data["blocks"][0]["block_uid"]
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_duplicate_block_uid_rejected():
    data = _minimal_raw()
    data["blocks"].append(dict(data["blocks"][0]))
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_pixel_bbox_rejected():
    data = _minimal_raw()
    data["blocks"][0]["bbox"] = [0, 0, 1000, 1000]
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_bbox_above_one_or_inverted_rejected():
    for bad in ([0, 0, 1.5, 0.5], [0.5, 0, 0.1, 0.1], [-0.1, 0, 0.5, 0.1]):
        data = _minimal_raw()
        data["blocks"][0]["bbox"] = bad
        with pytest.raises(ValidationError):
            validate_raw_document(data)


def test_null_bbox_accepted():
    # 实测无此情况，但页面尺寸缺失时 AnGIneer 会给 null，宽松接收
    data = _minimal_raw()
    data["blocks"][0]["bbox"] = None
    doc = validate_raw_document(data)
    assert doc.blocks[0].bbox is None


def test_source_vocabulary_enforced():
    # 实测词表 text/ocr/table/formula/null；v2 文档措辞 "native" 拒收
    data = _minimal_raw()
    data["blocks"][0]["source"] = "native"
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_null_source_and_confidence_accepted():
    data = _minimal_raw()
    data["blocks"][0]["source"] = None
    data["blocks"][0]["confidence"] = None
    doc = validate_raw_document(data)
    assert doc.blocks[0].source is None


def test_confidence_out_of_range_rejected():
    data = _minimal_raw()
    data["blocks"][0]["confidence"] = 1.5
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_docmeta_field_must_be_present_even_if_null():
    # v2 §5-7：可 null 不可省略
    data = _minimal_raw()
    del data["meta"]["docMeta"]["author"]
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_pages_required_and_positive_float():
    data = _minimal_raw()
    data["meta"]["pages"] = []
    with pytest.raises(ValidationError):
        validate_raw_document(data)
    data = _minimal_raw()
    data["meta"]["pages"] = [{"pageIdx": 0, "width": 0, "height": 825.0}]
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_table_html_purity_enforced():
    for bad_html in (
        '<table class="t"><tr><td>1</td></tr></table>',
        '<table><tr><td style="color:red">1</td></tr></table>',
        '<table><thead><tr><td>1</td></tr></thead></table>',
    ):
        data = _minimal_raw()
        data["blocks"][0]["block_type"] = "table"
        data["blocks"][0]["plain_text"] = ""
        data["blocks"][0]["table_html"] = bad_html
        data["blocks"][0]["image_path"] = "images/t.jpg"
        with pytest.raises(ValidationError):
            validate_raw_document(data)


def test_table_html_with_rowspan_colspan_passes():
    data = _minimal_raw()
    data["blocks"][0]["block_type"] = "table"
    data["blocks"][0]["plain_text"] = ""
    data["blocks"][0]["table_html"] = (
        '<table><tr><td rowspan="2">a</td><td colspan="2">b</td></tr>'
        "<tr><td>c</td><td>d</td></tr></table>"
    )
    data["blocks"][0]["image_path"] = "images/t.jpg"
    doc = validate_raw_document(data)
    assert doc.blocks[0].table_html is not None
```

运行确认失败（此时 `app/angineer/raw.py` 尚不存在，collection 报 ImportError 即视为失败）：

```bash
cd services/compare-algo && uv run pytest tests/test_angineer_raw.py -q
```

- [ ] **Step 2: 实现 `app/angineer/raw.py`**

`services/compare-algo/app/angineer/raw.py`：

```python
"""AnGIneer 原始产物 pydantic 模型（宽松校验）。

对应 doc_blocks_graph.jsonl 节点 + doc_blocks_graph_meta.json 的 {docMeta, outlines, pages}。
只校验适配层读取的字段，未知字段忽略（产物增补字段不破坏契约）。
docId 为 opaque 字符串（ABP 文档 id），不校验 block_uid 前缀。
校验失败抛 pydantic.ValidationError（含字段路径），由 API 层统一转 422。
"""
from __future__ import annotations

from typing import Literal, Optional

from lxml import html as lxml_html
from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

# table html 纯净结构（实测 132 表仅 table/tr/td + rowspan/colspan）
_ALLOWED_TABLE_TAGS = {"table", "tr", "td", "th"}
_ALLOWED_TABLE_ATTRS = {"rowspan", "colspan"}

# 实测词表（solo_engine.py:1225-1257）：原生文本 = "text"（v2 文档 "native" 系措辞）
RawSource = Literal["text", "ocr", "table", "formula"]


class RawBlock(BaseModel):
    model_config = ConfigDict(extra="ignore")

    block_uid: str = Field(min_length=1)
    block_type: str = Field(min_length=1)
    page_idx: int = Field(ge=0)
    block_seq: int = Field(default=0, ge=0)
    plain_text: Optional[str] = None
    # 0~1 归一化；页面尺寸缺失时 AnGIneer 给 null，宽松接收
    bbox: Optional[tuple[float, float, float, float]] = None
    derived_level: Optional[int] = None
    parent_uid: Optional[str] = None
    source: Optional[RawSource] = None
    confidence: Optional[float] = Field(default=None, ge=0.0, le=1.0)
    image_path: Optional[str] = None
    table_html: Optional[str] = None
    math_content: Optional[str] = None
    formula_body: Optional[str] = None
    formula_number: Optional[str] = None

    @field_validator("bbox")
    @classmethod
    def _check_bbox(cls, v):
        if v is None:
            return v
        x0, y0, x1, y1 = v
        if x0 < 0 or y0 < 0:
            raise ValueError(f"bbox {list(v)} 坐标不得为负")
        if x1 < x0 or y1 < y0:
            raise ValueError(f"bbox {list(v)} 必须满足 x1>=x0 且 y1>=y0")
        if max(v) > 1.0:
            raise ValueError(f"bbox {list(v)} 超出 0~1 归一化区间（疑似像素坐标）")
        return v

    @field_validator("table_html")
    @classmethod
    def _check_table_html_purity(cls, v):
        if v is None:
            return v
        try:
            root = lxml_html.fromstring(v)
        except Exception as exc:
            raise ValueError(f"table_html 不是合法 HTML：{exc}") from exc
        for el in root.iter():
            tag = el.tag if isinstance(el.tag, str) else ""
            if tag not in _ALLOWED_TABLE_TAGS:
                raise ValueError(f"table_html 含非法标签 <{tag}>，仅允许 table/tr/td/th")
            for attr in el.attrib:
                if attr not in _ALLOWED_TABLE_ATTRS:
                    raise ValueError(f"table_html 含非法属性 {attr!r}，仅允许 rowspan/colspan")
        return v


class RawDocMeta(BaseModel):
    model_config = ConfigDict(extra="ignore")

    # v2 §5-7：全部可 null 不可省略（Optional 无默认值 = 必填可空）
    fileName: Optional[str]
    pageCount: Optional[int]
    author: Optional[str]
    creatorTool: Optional[str]
    createdAt: Optional[str]
    modifiedAt: Optional[str]


class RawOutline(BaseModel):
    model_config = ConfigDict(extra="ignore")

    outline_id: Optional[str] = None
    title: str = ""
    level: Optional[int] = None
    page_idx: int = Field(ge=0)
    anchor_block_id: Optional[str] = None
    parent_outline_id: Optional[str] = None
    printed_page_label: Optional[str] = None


class RawPage(BaseModel):
    model_config = ConfigDict(extra="ignore")

    pageIdx: int = Field(ge=0)
    width: float = Field(gt=0)   # 实测为浮点（如 612.0）
    height: float = Field(gt=0)


class RawMeta(BaseModel):
    model_config = ConfigDict(extra="ignore")  # edges/stats/generated_at/build_id 忽略

    docMeta: RawDocMeta
    outlines: list[RawOutline] = Field(default_factory=list)
    pages: list[RawPage] = Field(min_length=1)


class RawDocumentEnvelope(BaseModel):
    """请求体 documents 元素：{docId, blocks, meta}。"""

    model_config = ConfigDict(extra="ignore")

    docId: str = Field(min_length=1)  # opaque：ABP 文档 id
    blocks: list[RawBlock] = Field(min_length=1)
    meta: RawMeta

    @model_validator(mode="after")
    def _check_block_uid_unique(self) -> "RawDocumentEnvelope":
        ids = [b.block_uid for b in self.blocks]
        dups = sorted({i for i in ids if ids.count(i) > 1})
        if dups:
            raise ValueError(f"block_uid 重复：{dups}")
        return self


def validate_raw_document(data: dict) -> RawDocumentEnvelope:
    """产物校验入口：不合格抛 pydantic.ValidationError（含具体字段路径）。"""
    return RawDocumentEnvelope.model_validate(data)
```

- [ ] **Step 3: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_angineer_raw.py -q
```

预期：全部 passed（含 4 份真实 fixture）。

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): AnGIneer 原始产物宽松校验 schema（实测词表/纯净表格/bbox 归一）"
```

---

## Task 4: 适配器与内部分析模型（adapter.py + pdf_date.py + ir.py + conftest）

**Files:**
- Create: `services/compare-algo/app/angineer/pdf_date.py`
- Create: `services/compare-algo/app/angineer/adapter.py`
- Create: `services/compare-algo/app/schemas/ir.py`
- Create: `services/compare-algo/tests/conftest.py`（合成 raw fixture + 真实 fixture 加载器，后续所有 Task 复用）
- Create: `services/compare-algo/tests/test_angineer_adapter.py`

适配规则（v2 §2/§3 + 实测事实裁决）：

| 内部模型 | 取值 |
|---|---|
| `blockId` | `block_uid` 原样 |
| `pageIdx` | `page_idx` |
| `bbox` | `bbox`（0~1 或 null，原样） |
| `type` | title→`title`，paragraph→`para`，list→`list`，table→`table`，equation_interline→`equation`，image/figure/**chart**→`image`，page_header→`header`，page_footer/page_number→`footer`；未知类型 → `para`（保守当正文，不丢内容） |
| `text` | `plain_text` 剥 HTML 标签；equation 取 `math_content` 或 `formula_body`（LaTeX） |
| `textLevel` | 标题块 = `derived_level`（null→0）；非标题固定 0 |
| `source`/`confidence` | 原样（text/ocr/table/formula/null） |
| `table.html`/`imgPath` | `table_html` / `image_path` |
| `imgPath`（image/equation） | `image_path` |
| `outline` | `meta.outlines` 扁平（parent_outline_id）→ 嵌套 `children`；`blockId` ← `anchor_block_id`；标题剥标签 |
| `meta.createdAt`/`modifiedAt` | PDF 原始日期 → ISO 8601（`pdf_date.parse_pdf_date`；ISO/不可解析原样保留，null→null） |
| `pages` | `meta.pages` 原样（真实 pt 尺寸，浮点） |

内部模型硬规则（适配输出必须满足，违例抛 ValidationError → API 层 422）：`blockId` 唯一；outline 引用必须存在；`pageIdx` 必须存在于 `pages`；table 必须有 `imgPath`（html 可选——实测 2/132 无 html）；image/equation 必须有 `imgPath`（实测全部有）；equation 的 `text` 必须非空（LaTeX）；`source="text"` 时 `confidence` 非 null 则必须 1.0。

- [ ] **Step 1: 写测试 fixture（conftest.py）与失败测试，运行确认失败**

`services/compare-algo/tests/conftest.py`：

```python
"""测试 fixture。

合成 fixture：3 份虚拟标书，以 **AnGIneer 原始产物形态**（snake_case）构造，
经 validate_raw_document + adapt_document 得到内部模型——全链路走适配层，
与生产路径一致（Task 16 的 ir_payload 直接复用 raw 形态）。

- doc-a / doc-b：共享一段雷同承诺文本（内含故意错别字「保证今」，各出现一次）、
  同一作者「张三」、同一 createdAt、报价 1,000,000 / 1,010,000（与 doc-c 构成等差）。
- doc-b 另有一个 source=ocr 且 confidence=0.3 的低置信块，文本与 doc-a 的 b004 完全相同，
  用于 OCR 降权测试（spec §4.5；真实数据 confidence 全 1.0，此场景只能靠合成）。
- doc-c：内容、作者、时间均独立，报价 1,020,000（三份构成公差 10,000 的等差数列）。
- 三份 creatorTool 均为 "Microsoft Word"（全相同 → 低危线索）。

真实 fixture：tests/fixtures/ 下 4 份裁剪产物（Task 2 生成），
raw_haigang_pair（部分雷同对）/ raw_pingshen_pair（元数据一致对）。
"""
import json
import pathlib

import pytest

from app.angineer.adapter import adapt_document
from app.angineer.raw import validate_raw_document
from app.schemas.ir import IrDocument

FIXTURES = pathlib.Path(__file__).parent / "fixtures"

PAGE = {"pageIdx": 0, "width": 1190.0, "height": 1684.0}

SHARED_PARAGRAPH = (
    "我公司郑重承诺，若我方中标，将在合同签订后三十个日历日内进场施工，"
    "严格按照招标文件要求的质量标准组织实施，并缴纳履约保证今壹佰万元整，"
    "确保工程按期保质完成，特此承诺。"
)
# 注：「保证今」为故意植入的错别字（金→今），A/B 各出现一次，全文仅此一次。

A_INTRO = "我司系市政一级资质施工企业。"
B_INTRO = "本单位具备建筑工程总承包特级资质。"
A_SEAL_LINE = "本承诺书由法定代表人签字并加盖公章后生效。"

C_TITLE = "技术方案响应"
C_PARA_1 = "本集团致力于智慧园区整体解决方案的研发与交付。"
C_PARA_2 = "我们拥有完全自主的知识产权与成熟实施经验。"


def make_raw_block(
    uid: str,
    text: str,
    *,
    block_type: str = "paragraph",
    source: str | None = "text",
    confidence: float | None = 1.0,
    page_idx: int = 0,
    seq: int = 1,
    y: int = 0,
    derived_level: int | None = None,
    table_html: str | None = None,
    image_path: str | None = None,
    math_content: str | None = None,
) -> dict:
    return {
        "block_uid": uid,
        "block_type": block_type,
        "page_idx": page_idx,
        "block_seq": seq,
        "plain_text": text,
        # AnGIneer bbox 为 0~1 归一化（此处由像素坐标 ÷ 页面尺寸 1190×1684 换算）
        "bbox": [50 / 1190, (100 + y) / 1684, 1140 / 1190, (140 + y) / 1684],
        "derived_level": derived_level,
        "parent_uid": None,
        "source": source,
        "confidence": confidence,
        "image_path": image_path,
        "table_html": table_html,
        "math_content": math_content,
        "formula_body": None,
        "formula_number": None,
    }


def price_table_html(total: str) -> str:
    return (
        "<table>"
        "<tr><td>项目</td><td>金额</td></tr>"
        "<tr><td>分部分项工程费</td><td>800,000.00</td></tr>"
        "<tr><td>措施费</td><td>100,000.00</td></tr>"
        f"<tr><td>投标总价（元）</td><td>{total}</td></tr>"
        "</table>"
    )


def make_raw_doc(
    doc_id: str,
    *,
    file_name: str,
    author: str | None,
    created_at: str | None,
    blocks: list[dict],
) -> dict:
    return {
        "docId": doc_id,
        "blocks": blocks,
        "meta": {
            "docMeta": {
                "fileName": file_name,
                "pageCount": 1,
                "author": author,
                "creatorTool": "Microsoft Word",
                "createdAt": created_at,
                "modifiedAt": None,
            },
            "outlines": [],
            "pages": [dict(PAGE)],
        },
    }


def adapt(raw: dict) -> IrDocument:
    """raw dict → 校验 → 适配 → 内部模型（测试统一入口，与生产路径一致）。"""
    return adapt_document(validate_raw_document(raw))


def load_raw_fixture(name: str) -> dict:
    return json.loads((FIXTURES / f"{name}.json").read_text(encoding="utf-8"))


@pytest.fixture()
def raw_doc_a() -> dict:
    return make_raw_doc(
        "doc-a",
        file_name="A公司投标文件.pdf",
        author="张三",
        created_at="2026-07-01T10:00:00",
        blocks=[
            make_raw_block("b001", "投标函", block_type="title", derived_level=1, y=0),
            make_raw_block("b002", A_INTRO, seq=2, y=60),
            make_raw_block("b003", SHARED_PARAGRAPH, seq=3, y=120),
            make_raw_block("b004", A_SEAL_LINE, seq=4, y=180),
            make_raw_block("b005", "", block_type="table", seq=5, y=240,
                           table_html=price_table_html("1,000,000.00"),
                           image_path="images/price.jpg"),
        ],
    )


@pytest.fixture()
def raw_doc_b() -> dict:
    return make_raw_doc(
        "doc-b",
        file_name="B公司投标文件.pdf",
        author="张三",
        created_at="2026-07-01T10:00:00",
        blocks=[
            make_raw_block("b001", "投标函", block_type="title", derived_level=1, y=0),
            make_raw_block("b002", B_INTRO, seq=2, y=60),
            make_raw_block("b003", SHARED_PARAGRAPH, seq=3, y=120),
            make_raw_block("b004", "", block_type="table", seq=4, y=180,
                           table_html=price_table_html("1,010,000.00"),
                           image_path="images/price.jpg"),
            # 低置信 OCR 块，文本与 doc-a 的 b004 完全相同 → 雷同但须降权（spec §4.5）
            make_raw_block("b005", A_SEAL_LINE, source="ocr", confidence=0.3, seq=5, y=240),
        ],
    )


@pytest.fixture()
def raw_doc_c() -> dict:
    return make_raw_doc(
        "doc-c",
        file_name="C公司投标文件.pdf",
        author="李四",
        created_at="2026-07-02T09:30:00",
        blocks=[
            make_raw_block("b001", C_TITLE, block_type="title", derived_level=1, y=0),
            make_raw_block("b002", C_PARA_1, seq=2, y=60),
            make_raw_block("b003", C_PARA_2, seq=3, y=120),
            make_raw_block("b004", "", block_type="table", seq=4, y=180,
                           table_html=price_table_html("1,020,000.00"),
                           image_path="images/price.jpg"),
        ],
    )


@pytest.fixture()
def ir_doc_a(raw_doc_a) -> IrDocument:
    return adapt(raw_doc_a)


@pytest.fixture()
def ir_doc_b(raw_doc_b) -> IrDocument:
    return adapt(raw_doc_b)


@pytest.fixture()
def ir_doc_c(raw_doc_c) -> IrDocument:
    return adapt(raw_doc_c)


@pytest.fixture()
def ir_docs(ir_doc_a, ir_doc_b, ir_doc_c) -> list[IrDocument]:
    return [ir_doc_a, ir_doc_b, ir_doc_c]


@pytest.fixture()
def ir_payload(raw_doc_a, raw_doc_b, raw_doc_c) -> dict:
    """可直接 POST 的请求体（raw 产物形态，Task 16 接口测试用）。"""
    return {"taskId": "task-001", "documents": [raw_doc_a, raw_doc_b, raw_doc_c]}


@pytest.fixture()
def raw_haigang_pair() -> list[dict]:
    return [load_raw_fixture("haigang1"), load_raw_fixture("haigang2")]


@pytest.fixture()
def raw_pingshen_pair() -> list[dict]:
    return [load_raw_fixture("pingshen_a"), load_raw_fixture("pingshen_b")]
```

`services/compare-algo/tests/test_angineer_adapter.py`：

```python
import pytest
from pydantic import ValidationError

from app.angineer.pdf_date import parse_pdf_date

from tests.conftest import adapt, make_raw_block, make_raw_doc


class TestPdfDate:
    def test_full_pdf_date(self):
        assert parse_pdf_date("D:20251229164720+08'00'") == "2025-12-29T16:47:20+08:00"

    def test_zulu_timezone(self):
        assert parse_pdf_date("D:20250102030405Z") == "2025-01-02T03:04:05+00:00"

    def test_partial_date_defaults(self):
        assert parse_pdf_date("D:2025") == "2025-01-01T00:00:00"

    def test_iso_passthrough(self):
        assert parse_pdf_date("2026-07-01T10:00:00") == "2026-07-01T10:00:00"

    def test_unparseable_passthrough(self):
        assert parse_pdf_date("not-a-date") == "not-a-date"

    def test_none_and_empty(self):
        assert parse_pdf_date(None) is None
        assert parse_pdf_date("  ") is None

    def test_trailing_junk_passthrough(self):
        # 尾部垃圾不静默吞掉：原样保留，避免不同垃圾串碰撞出相同日期（假阳性）
        assert parse_pdf_date("D:2025abc") == "D:2025abc"
        assert parse_pdf_date("D:20251229164720+08'00'junk") == "D:20251229164720+08'00'junk"

    def test_hour_only_timezone(self):
        # 小时级时区（分钟缺省）与 +08'00' 同一时刻，须产出同一字符串
        assert parse_pdf_date("D:20251229164720+08'") == "2025-12-29T16:47:20+08:00"


class TestTypeMapping:
    def test_paragraph_and_title(self, ir_doc_a):
        types = {b.blockId: b.type for b in ir_doc_a.blocks}
        assert types["b001"] == "title"
        assert types["b002"] == "para"

    def test_title_text_level_from_derived_level(self, ir_doc_a):
        title = next(b for b in ir_doc_a.blocks if b.type == "title")
        assert title.textLevel == 1
        para = next(b for b in ir_doc_a.blocks if b.type == "para")
        assert para.textLevel == 0

    def test_table_mapping(self, ir_doc_a):
        table = next(b for b in ir_doc_a.blocks if b.type == "table")
        assert table.table is not None
        assert table.table.html.startswith("<table>")
        assert table.table.imgPath == "images/price.jpg"

    def test_equation_uses_math_content(self):
        raw = make_raw_doc("d-eq", file_name="e.pdf", author=None, created_at=None, blocks=[
            make_raw_block("e1", "", block_type="equation_interline",
                           math_content="E=mc^2", image_path="images/e.png"),
        ])
        doc = adapt(raw)
        assert doc.blocks[0].type == "equation"
        assert doc.blocks[0].text == "E=mc^2"

    def test_chart_mapped_to_image(self):
        # 实测 chart：有 image_path、文本为空、source=null（v2 §3 补充映射）
        raw = make_raw_doc("d-chart", file_name="c.pdf", author=None, created_at=None, blocks=[
            make_raw_block("c1", "", block_type="chart", source=None, confidence=None,
                           image_path="images/chart.jpg"),
        ])
        doc = adapt(raw)
        assert doc.blocks[0].type == "image"
        assert doc.blocks[0].imgPath == "images/chart.jpg"

    def test_furniture_mapping(self):
        raw = make_raw_doc("d-f", file_name="f.pdf", author=None, created_at=None, blocks=[
            make_raw_block("h1", "页眉文本", block_type="page_header"),
            make_raw_block("f1", "45", block_type="page_number", seq=2, y=1500),
            make_raw_block("f2", "页脚文本", block_type="page_footer", seq=3, y=1520),
            make_raw_block("p1", "正文", seq=4, y=200),
        ])
        doc = adapt(raw)
        types = {b.blockId: b.type for b in doc.blocks}
        assert types["h1"] == "header"
        assert types["f1"] == "footer"  # page_number 归入 footer（v2 §3：归入或忽略）
        assert types["f2"] == "footer"

    def test_unknown_type_falls_back_to_para(self):
        raw = make_raw_doc("d-u", file_name="u.pdf", author=None, created_at=None, blocks=[
            make_raw_block("u1", "某种未知块内容", block_type="some_future_type"),
        ])
        doc = adapt(raw)
        assert doc.blocks[0].type == "para"
        assert doc.blocks[0].text == "某种未知块内容"


class TestTextSanitize:
    def test_html_tags_stripped(self):
        # 实测标题含 <sub> 等标签
        raw = make_raw_doc("d-h", file_name="h.pdf", author=None, created_at=None, blocks=[
            make_raw_block("t1", "2<sub>.</sub> 1 一般规定", block_type="title", derived_level=2),
        ])
        doc = adapt(raw)
        assert doc.blocks[0].text == "2. 1 一般规定"

    def test_non_tag_angle_brackets_preserved(self):
        # 标书常见「<±5%>」「5<x<10>」等非标签尖括号不得误剥
        raw = make_raw_doc("d-lt", file_name="lt.pdf", author=None, created_at=None, blocks=[
            make_raw_block("p1", "偏差<±5%>以内，当 5<x<10>y 时成立"),
        ])
        doc = adapt(raw)
        assert doc.blocks[0].text == "偏差<±5%>以内，当 5<x<10>y 时成立"

    def test_html_entities_unescaped(self):
        raw = make_raw_doc("d-amp", file_name="amp.pdf", author=None, created_at=None, blocks=[
            make_raw_block("p1", "A&amp;B 联合体"),
        ])
        doc = adapt(raw)
        assert doc.blocks[0].text == "A&B 联合体"


class TestMetaMapping:
    def test_pdf_date_normalized(self):
        raw = make_raw_doc("d-m", file_name="m.pdf", author=None,
                           created_at="D:20251229164720+08'00'", blocks=[
                               make_raw_block("p1", "正文"),
                           ])
        doc = adapt(raw)
        assert doc.meta.createdAt == "2025-12-29T16:47:20+08:00"

    def test_iso_date_passthrough(self, ir_doc_a):
        assert ir_doc_a.meta.createdAt == "2026-07-01T10:00:00"


class TestOutlineNesting:
    def test_flat_outlines_to_nested(self):
        raw = make_raw_doc("d-o", file_name="o.pdf", author=None, created_at=None, blocks=[
            make_raw_block("t1", "第一章", block_type="title", derived_level=1),
            make_raw_block("t2", "第一节", block_type="title", derived_level=2, seq=2, y=60),
            make_raw_block("p1", "正文", seq=3, y=120),
        ])
        raw["meta"]["outlines"] = [
            {"outline_id": "o1", "title": "第一章", "level": 1, "page_idx": 0,
             "anchor_block_id": "t1", "parent_outline_id": None, "printed_page_label": "1"},
            {"outline_id": "o2", "title": "第一节", "level": 2, "page_idx": 0,
             "anchor_block_id": "t2", "parent_outline_id": "o1", "printed_page_label": "1"},
        ]
        doc = adapt(raw)
        assert len(doc.outline) == 1
        assert doc.outline[0].blockId == "t1"
        assert doc.outline[0].children[0].blockId == "t2"

    def test_outline_parent_cycle_not_lost(self):
        # 父引用成环（A↔B）：拆环后全部保留为可到达节点，不静默丢失也不递归崩溃
        raw = make_raw_doc("d-cyc", file_name="cyc.pdf", author=None, created_at=None, blocks=[
            make_raw_block("tA", "章节A", block_type="title", derived_level=1),
            make_raw_block("tB", "章节B", block_type="title", derived_level=1, seq=2, y=60),
        ])
        raw["meta"]["outlines"] = [
            {"outline_id": "oA", "title": "章节A", "level": 1, "page_idx": 0,
             "anchor_block_id": "tA", "parent_outline_id": "oB", "printed_page_label": "1"},
            {"outline_id": "oB", "title": "章节B", "level": 1, "page_idx": 0,
             "anchor_block_id": "tB", "parent_outline_id": "oA", "printed_page_label": "1"},
        ]
        doc = adapt(raw)
        found = {n.blockId for n in doc.outline}
        found |= {c.blockId for n in doc.outline for c in n.children}
        assert found == {"tA", "tB"}


class TestInternalModelGuards:
    def test_table_requires_imgpath(self):
        raw = make_raw_doc("d-t", file_name="t.pdf", author=None, created_at=None, blocks=[
            make_raw_block("t1", "", block_type="table",
                           table_html="<table><tr><td>1</td></tr></table>"),
        ])
        with pytest.raises(ValidationError):
            adapt(raw)

    def test_table_without_html_allowed(self):
        # 实测 2/132 表格无 table_html（有整表截图）：合法，pricing 跳过
        raw = make_raw_doc("d-t2", file_name="t.pdf", author=None, created_at=None, blocks=[
            make_raw_block("p1", "正文"),
            make_raw_block("t1", "", block_type="table", seq=2, y=60,
                           image_path="images/t.jpg"),
        ])
        doc = adapt(raw)
        table = next(b for b in doc.blocks if b.type == "table")
        assert table.table is not None
        assert table.table.html is None
        assert table.table.imgPath == "images/t.jpg"

    def test_image_requires_imgpath(self):
        raw = make_raw_doc("d-i", file_name="i.pdf", author=None, created_at=None, blocks=[
            make_raw_block("i1", "", block_type="image"),
        ])
        with pytest.raises(ValidationError):
            adapt(raw)

    def test_equation_requires_latex(self):
        raw = make_raw_doc("d-e", file_name="e.pdf", author=None, created_at=None, blocks=[
            make_raw_block("e1", "", block_type="equation_interline",
                           image_path="images/e.png"),
        ])
        with pytest.raises(ValidationError):
            adapt(raw)

    def test_text_source_confidence_must_be_1(self):
        # source=text（原生文本）confidence 非 null 时必须 1.0
        raw = make_raw_doc("d-s", file_name="s.pdf", author=None, created_at=None, blocks=[
            make_raw_block("p1", "正文", source="text", confidence=0.8),
        ])
        with pytest.raises(ValidationError):
            adapt(raw)

    def test_page_idx_must_exist_in_pages(self):
        raw = make_raw_doc("d-p", file_name="p.pdf", author=None, created_at=None, blocks=[
            make_raw_block("p1", "正文", page_idx=5),
        ])
        with pytest.raises(ValidationError):
            adapt(raw)


class TestRealFixtures:
    """真实产物适配结果固化（实测值来自 Task 2 Step 2）。"""

    def test_haigang1_block_types_and_meta(self, raw_haigang_pair):
        doc = adapt(raw_haigang_pair[0])
        assert doc.docId == "doc-12f45ca9"
        assert len(doc.blocks) == 38
        from collections import Counter
        counts = Counter(b.type for b in doc.blocks)
        assert counts == {"para": 29, "title": 4, "header": 2, "footer": 2, "equation": 1}
        assert doc.meta.creatorTool == "Adobe Acrobat 9.3.2"
        assert doc.meta.createdAt == "2025-12-29T16:47:20+08:00"  # PDF 日期已归一
        assert len(doc.pages) == 2
        assert doc.pages[0].width == 612.0

    def test_haigang1_outline_nested(self, raw_haigang_pair):
        doc = adapt(raw_haigang_pair[0])
        assert len(doc.outline) == 1          # 1 根（第 6 章）
        assert len(doc.outline[0].children) == 3  # 6.1/6.2/6.3
        assert doc.outline[0].level == 1

    def test_haigang2_block_count(self, raw_haigang_pair):
        doc = adapt(raw_haigang_pair[1])
        assert doc.docId == "doc-c8be9f8b"
        assert len(doc.blocks) == 197

    def test_pingshen_pair_meta(self, raw_pingshen_pair):
        a, b = (adapt(d) for d in raw_pingshen_pair)
        assert a.meta.author and a.meta.author == b.meta.author
        assert a.meta.creatorTool == b.meta.creatorTool == "Writer"
        assert a.meta.createdAt != b.meta.createdAt  # 实测不同，不应出 createdAt 证据
```

运行确认失败（此时三个实现文件尚不存在，collection 报 ImportError 即视为失败）：

```bash
cd services/compare-algo && uv run pytest tests/test_angineer_adapter.py -q
```

- [ ] **Step 2: 实现 `app/angineer/pdf_date.py`**

`services/compare-algo/app/angineer/pdf_date.py`：

```python
"""PDF 原始日期归一：D:YYYYMMDDHHmmSSOHH'mm' → ISO 8601。

实测 docMeta.createdAt 为 PDF 原始日期串（如 D:20251229164720+08'00'）。
已是 ISO 或无法解析的输入原样返回（元数据比对是相等性分组，原样不影响正确性）。
"""
from __future__ import annotations

import re
from datetime import datetime, timedelta, timezone

# 全串锚定：尾部垃圾（如 D:2025abc）整体不匹配 → 原样保留，
# 避免不同垃圾串碰撞出同一日期（Task 14 createdAt 假阳性）；
# 时区分钟可缺省（+08'）：与 +08'00' 同一时刻须产出同一字符串（防假阴性）
_PDF_DATE_RE = re.compile(
    r"^D:(?P<y>\d{4})(?P<mo>\d{2})?(?P<d>\d{2})?"
    r"(?P<h>\d{2})?(?P<mi>\d{2})?(?P<s>\d{2})?"
    r"(?P<tz>[Zz]|[+-]\d{2}'?(?:\d{2}'?)?)?\s*$"
)


def parse_pdf_date(value: str | None) -> str | None:
    if value is None:
        return None
    text = value.strip()
    if not text:
        return None
    m = _PDF_DATE_RE.match(text)
    if not m:
        return value  # ISO 或其他格式：原样保留
    g = m.groupdict()
    try:
        dt = datetime(
            int(g["y"]), int(g["mo"] or 1), int(g["d"] or 1),
            int(g["h"] or 0), int(g["mi"] or 0), int(g["s"] or 0),
        )
    except ValueError:
        return value
    tz = g.get("tz")
    if tz:
        if tz in ("Z", "z"):
            dt = dt.replace(tzinfo=timezone.utc)
        else:
            sign = 1 if tz[0] == "+" else -1
            digits = re.sub(r"\D", "", tz[1:])
            hours = int(digits[:2] or 0)
            minutes = int(digits[2:4] or 0)
            dt = dt.replace(tzinfo=timezone(sign * timedelta(hours=hours, minutes=minutes)))
    return dt.isoformat()
```

- [ ] **Step 3: 实现 `app/schemas/ir.py`（内部分析模型）**

`services/compare-algo/app/schemas/ir.py`：

```python
"""内部分析模型（camelCase）：三个分析域（similarity/pricing/metadata）的唯一输入。

由 app/angineer/adapter.py 从 AnGIneer 产物适配生成，不直接出现在请求契约中。
校验规则是适配输出的底线守卫；产物级宽松校验在 app/angineer/raw.py。
"""
from __future__ import annotations

from typing import Literal, Optional

from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

# seal 为保留类型（spec §4.3.5）：AnGIneer 当前不产出
BlockType = Literal["title", "para", "table", "list", "image", "equation", "seal", "header", "footer"]
# 实测词表（AnGIneer solo_engine.py）：原生文本 = "text"
BlockSource = Literal["text", "ocr", "table", "formula"]


class IrMeta(BaseModel):
    model_config = ConfigDict(extra="forbid")

    fileName: Optional[str]
    pageCount: Optional[int]
    author: Optional[str]
    creatorTool: Optional[str]
    createdAt: Optional[str]   # 适配层已归一为 ISO 8601（或原样保留）
    modifiedAt: Optional[str]


class IrPage(BaseModel):
    model_config = ConfigDict(extra="forbid")

    pageIdx: int = Field(ge=0)
    width: float = Field(gt=0)   # 真实 pt 尺寸，浮点（如 612.0）
    height: float = Field(gt=0)


class IrOutlineNode(BaseModel):
    model_config = ConfigDict(extra="forbid")

    title: str
    level: int = Field(ge=1)
    blockId: str
    children: list["IrOutlineNode"] = Field(default_factory=list)


class IrTable(BaseModel):
    model_config = ConfigDict(extra="forbid")

    # 实测 2/132 表格无 table_html（有整表截图）：html 可选，imgPath 必须
    html: Optional[str] = None
    imgPath: str


class IrBlock(BaseModel):
    model_config = ConfigDict(extra="forbid")

    blockId: str = Field(min_length=1)  # = AnGIneer block_uid（v2 §2）
    pageIdx: int = Field(ge=0)
    bbox: Optional[tuple[float, float, float, float]] = None  # 0~1 归一化
    type: BlockType
    text: str = ""
    textLevel: int = Field(default=0, ge=0)
    # v2 §4：允许 null；为 null 时 OCR 降权自动关闭
    source: Optional[BlockSource] = None
    confidence: Optional[float] = Field(default=None, ge=0.0, le=1.0)
    table: Optional[IrTable] = None
    imgPath: Optional[str] = None

    @field_validator("bbox")
    @classmethod
    def _check_bbox_shape(cls, v):
        if v is None:
            return v
        x0, y0, x1, y1 = v
        if x0 < 0 or y0 < 0 or x1 < x0 or y1 < y0 or max(v) > 1.0:
            raise ValueError(f"bbox {list(v)} 非法（须 0~1 归一化且 x1>=x0, y1>=y0）")
        return v

    @model_validator(mode="after")
    def _check_type_requirements(self) -> "IrBlock":
        # source=text（原生文本）时 confidence 非 null 则必须 1.0
        if self.source == "text" and self.confidence is not None and self.confidence != 1.0:
            raise ValueError(f"block {self.blockId}：source=text 时 confidence 必须为 1.0")
        # table 必须给整表截图（html 可选：实测 2/132 无 html，pricing 跳过）
        if self.type == "table" and (self.table is None or not self.table.imgPath):
            raise ValueError(f"block {self.blockId}：type=table 必须提供 table.imgPath")
        if self.type != "table" and self.table is not None:
            raise ValueError(f"block {self.blockId}：非 table 类型不得携带 table 字段")
        # image / seal / equation 必须给 imgPath（实测全部有）
        if self.type in ("image", "seal", "equation") and not self.imgPath:
            raise ValueError(f"block {self.blockId}：type={self.type} 必须提供 imgPath")
        # 行间公式 text 必须给 LaTeX 源码（映射自 math_content/formula_body）
        if self.type == "equation" and not self.text.strip():
            raise ValueError(f"block {self.blockId}：equation 块的 text 必须给 LaTeX 源码")
        return self


class IrDocument(BaseModel):
    model_config = ConfigDict(extra="forbid")

    docId: str = Field(min_length=1)  # opaque：ABP 文档 id
    meta: IrMeta
    pages: list[IrPage] = Field(min_length=1)
    outline: list[IrOutlineNode] = Field(default_factory=list)
    blocks: list[IrBlock] = Field(min_length=1)

    @model_validator(mode="after")
    def _check_document(self) -> "IrDocument":
        # blockId 文档内唯一
        ids = [b.blockId for b in self.blocks]
        dups = sorted({i for i in ids if ids.count(i) > 1})
        if dups:
            raise ValueError(f"blockId 重复：{dups}")
        block_id_set = set(ids)
        # outline 引用的 blockId 必须存在
        def walk(nodes: list[IrOutlineNode]) -> list[IrOutlineNode]:
            out: list[IrOutlineNode] = []
            for n in nodes:
                out.append(n)
                out.extend(walk(n.children))
            return out
        for node in walk(self.outline):
            if node.blockId not in block_id_set:
                raise ValueError(f"outline 引用了不存在的 blockId：{node.blockId}")
        # pageIdx 必须存在；pages 保留页面真实尺寸（前端还原/打印用，不参与 bbox 校验）
        page_map = {p.pageIdx: p for p in self.pages}
        if len(page_map) != len(self.pages):
            raise ValueError("pages 中 pageIdx 重复")
        for b in self.blocks:
            if b.pageIdx not in page_map:
                raise ValueError(f"block {b.blockId} 的 pageIdx={b.pageIdx} 在 pages 中不存在")
        return self
```

- [ ] **Step 4: 实现 `app/angineer/adapter.py`**

`services/compare-algo/app/angineer/adapter.py`：

```python
"""AnGIneer 产物 → 内部分析模型 适配器。

全部产物字段知识收敛在 app/angineer/ 包内；下游引擎只消费 IrDocument。
适配输出经 IrDocument 校验，违例抛 pydantic.ValidationError（API 层转 422）。
"""
from __future__ import annotations

import html
import re

from app.angineer.pdf_date import parse_pdf_date
from app.angineer.raw import RawBlock, RawDocumentEnvelope, RawOutline
from app.schemas.ir import (
    IrBlock,
    IrDocument,
    IrMeta,
    IrOutlineNode,
    IrPage,
    IrTable,
)

# v2 §3 类型映射 + 实测补充：chart→image（v2 表遗漏，实测存在）；
# page_number→footer（v2：归入 header/footer 或忽略）；未知类型→para（不丢内容）
_TYPE_MAP = {
    "title": "title",
    "paragraph": "para",
    "list": "list",
    "table": "table",
    "equation_interline": "equation",
    "image": "image",
    "figure": "image",
    "chart": "image",
    "page_header": "header",
    "page_footer": "footer",
    "page_number": "footer",
}

# 标签名必需的标签匹配：剥 <sub>/<br/> 等真标签，
# 保留「偏差<±5%>」「5<x<10>」等非标签尖括号（标书常见，误剥会损毁正文）
_TAG_RE = re.compile(r"</?[a-zA-Z][a-zA-Z0-9]*(?:\s[^>]*)?/?>")


def _strip_html(text: str) -> str:
    """剥除 HTML 标签（实测标题 plain_text 含 <sub> 等），并解码实体（&amp; → &）。"""
    return html.unescape(_TAG_RE.sub("", text))


def _block_text(block: RawBlock) -> str:
    if block.block_type == "equation_interline":
        # 行间公式给 LaTeX 源码（math_content 含 \tag 编号，formula_body 更纯净，优先）
        return (block.formula_body or block.math_content or "").strip()
    return _strip_html(block.plain_text or "")


def _adapt_block(block: RawBlock) -> IrBlock:
    mapped_type = _TYPE_MAP.get(block.block_type, "para")
    table = None
    img_path = None
    if mapped_type == "table":
        table = IrTable(html=block.table_html, imgPath=block.image_path or "")
    elif mapped_type in ("image", "equation"):
        img_path = block.image_path
    text_level = 0
    if mapped_type == "title" and block.derived_level:
        text_level = block.derived_level
    return IrBlock(
        blockId=block.block_uid,
        pageIdx=block.page_idx,
        bbox=block.bbox,
        type=mapped_type,
        text=_block_text(block),
        textLevel=text_level,
        source=block.source,
        confidence=block.confidence,
        table=table,
        imgPath=img_path,
    )


def _nest_outlines(outlines: list[RawOutline]) -> list[IrOutlineNode]:
    """AnGIneer 扁平 outlines（parent_outline_id）→ 嵌套 children（v2 §5-6 二选一）。"""
    nodes: dict[str, IrOutlineNode] = {}
    order: list[str] = []
    for o in outlines:
        if not o.anchor_block_id:
            continue  # 无锚点的 outline 无法定位，跳过
        oid = o.outline_id or f"__auto_{len(order)}"
        nodes[oid] = IrOutlineNode(
            title=_strip_html(o.title),
            level=max(1, o.level or 1),
            blockId=o.anchor_block_id,
        )
        order.append(oid)
    roots: list[IrOutlineNode] = []
    parent_of: dict[str, str] = {}
    for o, oid in zip((o for o in outlines if o.anchor_block_id), order):
        parent = o.parent_outline_id
        if parent and parent in nodes and parent != oid:
            # 环检测：parent 祖先链含 oid 则挂载成环（IrDocument 校验会递归崩溃）→ 不挂，提升为根
            cur: str | None = parent
            is_cycle = False
            while cur is not None:
                if cur == oid:
                    is_cycle = True
                    break
                cur = parent_of.get(cur)
            if not is_cycle:
                nodes[parent].children.append(nodes[oid])
                parent_of[oid] = parent
                continue
        roots.append(nodes[oid])
    # 兜底：未挂在任何根下的节点（成环/悬空）提升为根，不静默丢失
    reachable: set[int] = set()

    def _collect(node: IrOutlineNode) -> None:
        if id(node) in reachable:
            return
        reachable.add(id(node))
        for child in node.children:
            _collect(child)

    for root in roots:
        _collect(root)
    for oid in order:
        if id(nodes[oid]) not in reachable:
            roots.append(nodes[oid])
    return roots


def adapt_document(raw: RawDocumentEnvelope) -> IrDocument:
    """产物 → 内部分析模型。输出经 IrDocument 校验，违例抛 ValidationError。"""
    meta = raw.meta.docMeta
    return IrDocument(
        docId=raw.docId,
        meta=IrMeta(
            fileName=meta.fileName,
            pageCount=meta.pageCount,
            author=meta.author,
            creatorTool=meta.creatorTool,
            createdAt=parse_pdf_date(meta.createdAt),
            modifiedAt=parse_pdf_date(meta.modifiedAt),
        ),
        pages=[IrPage(pageIdx=p.pageIdx, width=p.width, height=p.height) for p in raw.meta.pages],
        outline=_nest_outlines(raw.meta.outlines),
        blocks=[_adapt_block(b) for b in raw.blocks],
    )
```

- [ ] **Step 5: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_angineer_adapter.py -q
```

预期：全部 passed（含真实 fixture 断言）。

- [ ] **Step 6: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): AnGIneer 产物适配层（类型映射/PDF 日期/outline 嵌套）与内部分析模型"
```

## Task 5: Evidence 模型与组装器（spec §6.1 字段逐字）

**Files:**
- Create: `services/compare-algo/app/schemas/evidence.py`
- Create: `services/compare-algo/tests/test_evidence.py`

- [ ] **Step 1: 写失败测试**

`services/compare-algo/tests/test_evidence.py`：

```python
import pytest
from pydantic import ValidationError

from app.schemas.evidence import Evidence, EvidenceLocation, build_evidence


def _make() -> Evidence:
    return build_evidence(
        task_id="task-001",
        type="similarity",
        severity="high",
        doc_ids=["doc-a", "doc-b"],
        locations=[
            EvidenceLocation(docId="doc-a", blockIds=["b003"]),
            EvidenceLocation(docId="doc-b", blockIds=["b003"]),
        ],
        metrics={"similarity": 0.86},
        title="doc-a 与 doc-b 存在文本雷同",
        description="两段文档存在大段雷同。",
    )


def test_build_evidence_fields_match_spec():
    e = _make()
    assert e.taskId == "task-001"
    assert e.type == "similarity"
    assert e.severity == "high"
    assert e.docIds == ["doc-a", "doc-b"]
    assert e.metrics["similarity"] == 0.86
    # spec §3.2：本服务只产出确定性证据
    assert e.aiGenerated is False
    assert e.id


def test_build_evidence_generates_unique_ids():
    assert _make().id != _make().id


def test_metadata_evidence_may_have_empty_block_ids():
    # 元数据类证据无块级定位，locations 仍按文档给出
    e = build_evidence(
        task_id="task-001",
        type="metadata",
        severity="mid",
        doc_ids=["doc-a", "doc-b"],
        locations=[EvidenceLocation(docId="doc-a"), EvidenceLocation(docId="doc-b")],
        metrics={"field": "author", "value": "张三"},
        title="2 份标书文件作者相同（张三）",
        description="文件元数据作者一致。",
    )
    assert e.locations[0].blockIds == []


def test_invalid_type_rejected():
    with pytest.raises(ValidationError):
        build_evidence(
            task_id="task-001",
            type="unknown",
            severity="high",
            doc_ids=["doc-a"],
            locations=[EvidenceLocation(docId="doc-a")],
            metrics={},
            title="t",
            description="d",
        )


def test_invalid_severity_rejected():
    with pytest.raises(ValidationError):
        build_evidence(
            task_id="task-001",
            type="similarity",
            severity="critical",
            doc_ids=["doc-a"],
            locations=[EvidenceLocation(docId="doc-a")],
            metrics={},
            title="t",
            description="d",
        )


def test_full_type_literal_accepts_clause_and_indicator():
    # spec §6.1 契约含 clause/indicator（由 compare-ai 产出），模型须可表示
    for t in ("clause", "indicator"):
        e = Evidence(
            id="x",
            taskId="task-001",
            type=t,
            severity="low",
            docIds=["doc-a"],
            locations=[EvidenceLocation(docId="doc-a", blockIds=["b001"])],
            metrics={},
            title="t",
            description="d",
            aiGenerated=True,
        )
        assert e.type == t
```

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_evidence.py -q
```

- [ ] **Step 2: 实现 `app/schemas/evidence.py`**

`services/compare-algo/app/schemas/evidence.py`：

```python
"""Evidence 模型：字段名逐字遵守 spec §6.1。

本服务只通过 build_evidence 产出 aiGenerated=false 的
similarity / pricing / metadata 三类证据；clause / indicator 属 compare-ai。
"""
from __future__ import annotations

from typing import Any, Literal
from uuid import uuid4

from pydantic import BaseModel, Field

EvidenceType = Literal["similarity", "pricing", "metadata", "clause", "indicator"]
Severity = Literal["high", "mid", "low"]


class EvidenceLocation(BaseModel):
    docId: str
    # 元数据类证据无块级定位，允许空列表（spec §6.1 未约束非空）
    blockIds: list[str] = Field(default_factory=list)


class Evidence(BaseModel):
    id: str
    taskId: str
    type: EvidenceType
    severity: Severity
    docIds: list[str] = Field(min_length=1)
    locations: list[EvidenceLocation] = Field(min_length=1)
    metrics: dict[str, Any] = Field(default_factory=dict)
    title: str
    description: str
    aiGenerated: bool


def build_evidence(
    *,
    task_id: str,
    type: EvidenceType,
    severity: Severity,
    doc_ids: list[str],
    locations: list[EvidenceLocation],
    metrics: dict[str, Any] | None = None,
    title: str,
    description: str,
) -> Evidence:
    """组装确定性证据：id 自动生成，aiGenerated 恒为 False（spec §3.2）。"""
    return Evidence(
        id=str(uuid4()),
        taskId=task_id,
        type=type,
        severity=severity,
        docIds=doc_ids,
        locations=locations,
        metrics=metrics or {},
        title=title,
        description=description,
        aiGenerated=False,
    )
```

- [ ] **Step 3: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_evidence.py -q
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): Evidence 模型与 build_evidence 组装器（spec §6.1 字段逐字）"
```

---

## Task 6: OCR 低置信降权工具（spec §4.5）

**Files:**
- Create: `services/compare-algo/app/ocr.py`
- Create: `services/compare-algo/tests/test_ocr.py`

- [ ] **Step 1: 写失败测试**

`services/compare-algo/tests/test_ocr.py`：

```python
from app.ocr import (
    OCR_LOW_CONFIDENCE_THRESHOLD,
    downgrade_severity,
    is_low_confidence_ocr,
    low_confidence_ocr_block_ids,
)


def test_threshold_is_0_5():
    # spec §4.5：confidence<0.5 降权
    assert OCR_LOW_CONFIDENCE_THRESHOLD == 0.5


def test_is_low_confidence_ocr(ir_doc_a, ir_doc_b):
    a_b4 = next(b for b in ir_doc_a.blocks if b.blockId == "b004")
    b_b5 = next(b for b in ir_doc_b.blocks if b.blockId == "b005")
    b_b3 = next(b for b in ir_doc_b.blocks if b.blockId == "b003")
    assert is_low_confidence_ocr(a_b4) is False      # source=text（原生）
    assert is_low_confidence_ocr(b_b5) is True       # ocr + 0.3
    assert is_low_confidence_ocr(b_b3) is False      # source=text（原生）


def test_null_source_confidence_block_not_low(ir_doc_a):
    # v2 §4：source/confidence 为 null 的块不参与降权（AnGIneer 补齐前降级关闭）
    block = ir_doc_a.blocks[0].model_copy(update={"source": None, "confidence": None})
    assert is_low_confidence_ocr(block) is False


def test_low_confidence_ocr_block_ids(ir_doc_b):
    assert low_confidence_ocr_block_ids(ir_doc_b) == {"b005"}


def test_downgrade_severity():
    assert downgrade_severity("high") == "mid"
    assert downgrade_severity("mid") == "low"
    assert downgrade_severity("low") == "low"  # 触底不再降
```

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_ocr.py -q
```

- [ ] **Step 2: 实现 `app/ocr.py`**

`services/compare-algo/app/ocr.py`：

```python
"""OCR 低置信降权（spec §4.5）。

原生文本中「错得一样」是围标强证据；OCR 文本中可能只是识别器犯了同样的错。
因此 source=ocr 且 confidence<0.5 的块单独标注、证据降一级。
"""
from app.schemas.evidence import Severity
from app.schemas.ir import IrBlock, IrDocument

OCR_LOW_CONFIDENCE_THRESHOLD = 0.5

_SEVERITY_ORDER: list[Severity] = ["low", "mid", "high"]


def is_low_confidence_ocr(block: IrBlock) -> bool:
    # v2 §4：source/confidence 允许 null，缺失时不参与降权（不误伤）
    return (
        block.source == "ocr"
        and block.confidence is not None
        and block.confidence < OCR_LOW_CONFIDENCE_THRESHOLD
    )


def low_confidence_ocr_block_ids(doc: IrDocument) -> set[str]:
    return {b.blockId for b in doc.blocks if is_low_confidence_ocr(b)}


def downgrade_severity(severity: Severity) -> Severity:
    """严重度降一级，low 触底不变。"""
    idx = _SEVERITY_ORDER.index(severity)
    return _SEVERITY_ORDER[max(0, idx - 1)]
```

- [ ] **Step 3: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_ocr.py -q
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): OCR 低置信判定与 severity 降级工具（spec §4.5）"
```

---

## Task 7: 文本规范化与字级 n-gram shingling

**Files:**
- Create: `services/compare-algo/app/similarity/shingle.py`
- Create: `services/compare-algo/tests/test_shingle.py`

- [ ] **Step 1: 写失败测试**

`services/compare-algo/tests/test_shingle.py`：

```python
from app.similarity.shingle import (
    DEFAULT_NGRAM,
    block_shingles,
    char_ngrams,
    jaccard,
    normalize_text,
)


def test_default_ngram_is_3():
    assert DEFAULT_NGRAM == 3


def test_normalize_text_strips_whitespace_and_punctuation():
    assert normalize_text("我公司 郑重承诺，\n若中标。") == "我公司郑重承诺若中标"
    assert normalize_text("E = mc^2") == "Emc2"
    assert normalize_text("，。！？") == ""


def test_char_ngrams_basic():
    assert char_ngrams("abcd", 3) == {"abc", "bcd"}
    assert char_ngrams("abcde", 2) == {"ab", "bc", "cd", "de"}


def test_char_ngrams_short_text_returns_whole():
    assert char_ngrams("甲乙", 3) == {"甲乙"}
    assert char_ngrams("", 3) == set()


def test_block_shingles_skips_non_text_types(ir_doc_a):
    table_block = next(b for b in ir_doc_a.blocks if b.type == "table")
    assert block_shingles(table_block) == set()
    para = next(b for b in ir_doc_a.blocks if b.blockId == "b002")
    assert block_shingles(para) == char_ngrams(para.text)


def test_block_shingles_skips_furniture():
    # header/footer（页眉页脚页码）不查重：真实数据中不同文档常共享同一规范名页眉，
    # 参与会产生伪雷同（实测海港1/海港2 页眉相同）
    class _B:
        type = "header"
        text = "海港航道设计规范"
    assert block_shingles(_B()) == set()


def test_block_shingles_includes_equation_latex():
    class _B:
        type = "equation"
        text = "E=mc^2"
    assert block_shingles(_B()) == char_ngrams("E=mc^2")


def test_jaccard():
    assert jaccard({"a", "b"}, {"b", "c"}) == 1 / 3
    assert jaccard(set(), set()) == 0.0
    assert jaccard({"a"}, set()) == 0.0
    assert jaccard({"a", "b"}, {"a", "b"}) == 1.0
```

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_shingle.py -q
```

- [ ] **Step 2: 实现 `app/similarity/shingle.py`**

`services/compare-algo/app/similarity/shingle.py`：

```python
"""段落级 n-gram shingling：中文按字 trigram（tech 决策：bigram/trigram）。

规范化剔除空白与标点，只保留 CJK 表意文字与字母数字，
使排版/换行差异不影响查重。equation 块的 LaTeX 源码同样参与（spec §4.3.6）。
"""
import re

_KEEP_RE = re.compile(r"[一-鿿A-Za-z0-9]+")

# 参与文本查重的块类型；table 由 pricing 域单独处理，header/footer（页眉页脚页码）不查重
# （实测：不同文档常共享同一规范名页眉，参与会产生伪雷同）
SHINGLABLE_TYPES = ("title", "para", "list", "equation")

DEFAULT_NGRAM = 3


def normalize_text(text: str) -> str:
    return "".join(_KEEP_RE.findall(text))


def char_ngrams(text: str, n: int = DEFAULT_NGRAM) -> set[str]:
    """对规范化文本取字级 n-gram；长度不足 n 时整体作为唯一 gram。"""
    norm = normalize_text(text)
    if not norm:
        return set()
    if len(norm) <= n:
        return {norm}
    return {norm[i : i + n] for i in range(len(norm) - n + 1)}


def block_shingles(block, n: int = DEFAULT_NGRAM) -> set[str]:
    """块的 shingle 集合；不参与查重的块类型返回空集。"""
    if block.type not in SHINGLABLE_TYPES:
        return set()
    return char_ngrams(block.text, n)


def jaccard(a: set[str], b: set[str]) -> float:
    union = a | b
    if not union:
        return 0.0
    return len(a & b) / len(union)
```

- [ ] **Step 3: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_shingle.py -q
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): 文本规范化与字级 n-gram shingling"
```

---

## Task 8: MinHash/LSH 粗筛候选块对

**Files:**
- Create: `services/compare-algo/app/similarity/minhash.py`
- Create: `services/compare-algo/tests/test_minhash.py`

- [ ] **Step 1: 写失败测试**

`services/compare-algo/tests/test_minhash.py`：

```python
from app.similarity.minhash import (
    NUM_PERM,
    build_block_index,
    build_minhash,
    find_candidate_pairs,
)


def test_build_minhash_deterministic():
    s = {"abc", "bcd", "cde"}
    m1 = build_minhash(s)
    m2 = build_minhash(s)
    assert m1.hashvalues.tolist() == m2.hashvalues.tolist()
    assert len(m1.hashvalues) == NUM_PERM


def test_build_block_index_only_shinglable_blocks(ir_docs):
    index = build_block_index(ir_docs)
    keys = {(k.doc_id, k.block_id) for k in index}
    # table 块不进索引
    assert ("doc-a", "b005") not in keys
    assert ("doc-b", "b004") not in keys
    # para/title 块进索引（含 doc-b 的 OCR 块 b005）
    assert ("doc-a", "b003") in keys
    assert ("doc-b", "b005") in keys


def test_find_candidate_pairs_hits_identical_paragraph(ir_docs):
    pairs = find_candidate_pairs(build_block_index(ir_docs))
    hit = [p for p in pairs if {p.doc_id_a, p.doc_id_b} == {"doc-a", "doc-b"}
           and {p.block_id_a, p.block_id_b} == {"b003"}]
    assert len(hit) == 1
    assert hit[0].jaccard == 1.0


def test_find_candidate_pairs_cross_doc_only(ir_docs):
    pairs = find_candidate_pairs(build_block_index(ir_docs))
    assert all(p.doc_id_a != p.doc_id_b for p in pairs)


def test_find_candidate_pairs_no_doc_c_involved(ir_docs):
    # doc-c 内容完全独立，不应产生任何候选对
    pairs = find_candidate_pairs(build_block_index(ir_docs))
    assert all("doc-c" not in (p.doc_id_a, p.doc_id_b) for p in pairs)


def test_find_candidate_pairs_ocr_block_is_candidate(ir_docs):
    # doc-b b005（OCR 低置信）与 doc-a b004 文本相同 → 仍是候选（降权在 Task 11 处理）
    pairs = find_candidate_pairs(build_block_index(ir_docs))
    assert any({p.block_id_a, p.block_id_b} == {"b004", "b005"}
               and {p.doc_id_a, p.doc_id_b} == {"doc-a", "doc-b"} for p in pairs)
```

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_minhash.py -q
```

- [ ] **Step 2: 实现 `app/similarity/minhash.py`**

`services/compare-algo/app/similarity/minhash.py`：

```python
"""MinHash + LSH 粗筛：把 O(N²) 的块对比较降为近邻召回，再用精确 Jaccard 复核。"""
from collections import namedtuple

from datasketch import LeanMinHash, MinHash, MinHashLSH

from app.similarity.shingle import DEFAULT_NGRAM, block_shingles, jaccard

NUM_PERM = 128
LSH_THRESHOLD = 0.5      # LSH 召回阈值（近似 Jaccard）
CANDIDATE_JACCARD = 0.5  # 精确 Jaccard 复核阈值

BlockKey = namedtuple("BlockKey", ["doc_id", "block_id"])
CandidatePair = namedtuple(
    "CandidatePair", ["doc_id_a", "block_id_a", "doc_id_b", "block_id_b", "jaccard"]
)


def build_minhash(shingles: set[str], num_perm: int = NUM_PERM) -> LeanMinHash:
    mh = MinHash(num_perm=num_perm)
    for s in shingles:
        mh.update(s.encode("utf-8"))
    return LeanMinHash(mh)


def build_block_index(documents, n: int = DEFAULT_NGRAM, num_perm: int = NUM_PERM) -> dict:
    """为所有可查重块构建 {BlockKey: (shingles, LeanMinHash)} 索引。"""
    index: dict[BlockKey, tuple[set[str], LeanMinHash]] = {}
    for doc in documents:
        for block in doc.blocks:
            shingles = block_shingles(block, n)
            if shingles:
                index[BlockKey(doc.docId, block.blockId)] = (
                    shingles,
                    build_minhash(shingles, num_perm),
                )
    return index


def find_candidate_pairs(
    index: dict,
    threshold: float = LSH_THRESHOLD,
    min_jaccard: float = CANDIDATE_JACCARD,
) -> list[CandidatePair]:
    """LSH 粗筛跨文档候选块对，再用精确 Jaccard 复核。只保留跨文档对。"""
    lsh = MinHashLSH(threshold=threshold, num_perm=NUM_PERM)
    keys = list(index.keys())
    str_to_key = {f"{k.doc_id}/{k.block_id}": k for k in keys}
    with lsh.insertion_session() as session:
        for key in keys:
            session.insert(f"{key.doc_id}/{key.block_id}", index[key][1])

    seen: set[tuple[str, str]] = set()
    pairs: list[CandidatePair] = []
    for key in keys:
        skey = f"{key.doc_id}/{key.block_id}"
        for other_s in lsh.query(index[key][1]):
            other = str_to_key[other_s]
            if other.doc_id == key.doc_id:
                continue  # 只做跨文档两两比对
            pair_key = tuple(sorted((skey, other_s)))
            if pair_key in seen:
                continue
            seen.add(pair_key)
            jac = jaccard(index[key][0], index[other][0])
            if jac >= min_jaccard:
                pairs.append(
                    CandidatePair(key.doc_id, key.block_id, other.doc_id, other.block_id, round(jac, 4))
                )
    return pairs
```

- [ ] **Step 3: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_minhash.py -q
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): datasketch MinHash/LSH 粗筛跨文档候选块对"
```

## Task 9: 块级精确对齐与相似度计算（difflib 单调对齐）

**Files:**
- Create: `services/compare-algo/app/similarity/align.py`
- Create: `services/compare-algo/tests/test_align.py`

设计：候选块对按 A 侧阅读顺序排列后，用 `difflib.SequenceMatcher` 在 B 侧位置序列上取最长公共子序列（等价最长递增子序列），剔除交叉冲突的匹配；相似度为 Dice 系数 =（双方匹配字符数之和）/（双方可查重字符数之和），全文完全雷同时为 1.0。

- [ ] **Step 1: 写失败测试**

`services/compare-algo/tests/test_align.py`：

```python
from app.similarity.align import PairSimilarityResult, align_document_pair
from app.similarity.minhash import build_block_index, find_candidate_pairs


def _align(ir_doc_a, ir_doc_b) -> PairSimilarityResult:
    index = build_block_index([ir_doc_a, ir_doc_b])
    pairs = [
        p for p in find_candidate_pairs(index)
        if {p.doc_id_a, p.doc_id_b} == {"doc-a", "doc-b"}
    ]
    return align_document_pair(ir_doc_a, ir_doc_b, pairs)


def test_align_fixture_pair(ir_doc_a, ir_doc_b):
    r = _align(ir_doc_a, ir_doc_b)
    assert r.doc_id_a == "doc-a"
    assert r.doc_id_b == "doc-b"
    # 共享段（76 字）+ 承诺书行（21 字）+ 标题（3 字），占双方约 100/115 字符
    assert r.similarity > 0.8
    matched_a = {m.block_id_a for m in r.matches}
    matched_b = {m.block_id_b for m in r.matches}
    assert {"b001", "b003", "b004"} <= matched_a
    assert {"b001", "b003", "b005"} <= matched_b


def test_align_identical_docs_similarity_1(ir_doc_a):
    # 手工构造“与自身完全雷同”的候选对（模拟 ABP 重传同一文档的场景）：
    # 全部可查重块都匹配时 Dice = 1.0
    from app.similarity.minhash import CandidatePair
    clone = ir_doc_a.model_copy(update={"docId": "doc-a-copy"})
    pairs = [
        CandidatePair("doc-a", b.blockId, "doc-a-copy", b.blockId, 1.0)
        for b in ir_doc_a.blocks
        if b.type in ("title", "para", "list", "equation") and b.text
    ]
    r = align_document_pair(ir_doc_a, clone, pairs)
    assert r.similarity == 1.0


def test_align_no_pairs_gives_zero(ir_doc_a, ir_doc_c):
    r = align_document_pair(ir_doc_a, ir_doc_c, [])
    assert r.similarity == 0.0
    assert r.matches == []


def test_align_drops_crossing_matches(ir_doc_a, ir_doc_b):
    # 人为制造交叉匹配：b002(后)↔b003(前) 与 b003(前)↔b002(后) 不可同时成立，
    # 单调对齐必须只保留其中一条链
    from app.similarity.minhash import CandidatePair
    pairs = [
        CandidatePair("doc-a", "b002", "doc-b", "b003", 0.6),
        CandidatePair("doc-a", "b003", "doc-b", "b002", 0.6),
    ]
    r = align_document_pair(ir_doc_a, ir_doc_b, pairs)
    assert len(r.matches) == 1
```

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_align.py -q
```

- [ ] **Step 2: 实现 `app/similarity/align.py`**

`services/compare-algo/app/similarity/align.py`：

```python
"""块级精确对齐与相似度计算。

候选块对按 A 侧阅读顺序排列后，用 difflib.SequenceMatcher 在 B 侧位置序列上
取最长公共子序列（即最长单调递增匹配链），剔除交叉冲突。
相似度为 Dice 系数：(matched_a + matched_b) / (total_a + total_b)，按规范化字符数计。
"""
from dataclasses import dataclass, field
from difflib import SequenceMatcher

from app.schemas.ir import IrDocument
from app.similarity.minhash import CandidatePair
from app.similarity.shingle import SHINGLABLE_TYPES, normalize_text


@dataclass
class BlockMatch:
    block_id_a: str
    block_id_b: str
    jaccard: float


@dataclass
class PairSimilarityResult:
    doc_id_a: str
    doc_id_b: str
    similarity: float
    matches: list[BlockMatch] = field(default_factory=list)


def _shinglable_positions(doc: IrDocument) -> dict[str, int]:
    """blockId -> 在可查重块序列中的序号（保持阅读顺序，spec §4.3.7 段落级）。"""
    positions: dict[str, int] = {}
    for block in doc.blocks:
        if block.type in SHINGLABLE_TYPES and normalize_text(block.text):
            positions[block.blockId] = len(positions)
    return positions


def align_document_pair(
    doc_a: IrDocument,
    doc_b: IrDocument,
    pairs: list[CandidatePair],
) -> PairSimilarityResult:
    pos_a = _shinglable_positions(doc_a)
    pos_b = _shinglable_positions(doc_b)
    norm_len_a = {b.blockId: len(normalize_text(b.text)) for b in doc_a.blocks}
    norm_len_b = {b.blockId: len(normalize_text(b.text)) for b in doc_b.blocks}

    # 按 A 侧阅读顺序排列候选对，在 B 侧位置序列上取最长单调链
    ordered = sorted(pairs, key=lambda p: (pos_a.get(p.block_id_a, 0), pos_b.get(p.block_id_b, 0)))
    seq_b = [pos_b.get(p.block_id_b, -1) for p in ordered]
    keep: set[int] = set()
    if ordered:
        sm = SequenceMatcher(None, sorted(seq_b), seq_b, autojunk=False)
        for m in sm.get_matching_blocks():
            for k in range(m.size):
                keep.add(m.b + k)  # m.b 是 seq_b（即 ordered）中的下标

    matches = [
        BlockMatch(p.block_id_a, p.block_id_b, p.jaccard)
        for i, p in enumerate(ordered)
        if i in keep
    ]

    matched_a = {m.block_id_a for m in matches}
    matched_b = {m.block_id_b for m in matches}
    chars_a = sum(norm_len_a[bid] for bid in matched_a)
    chars_b = sum(norm_len_b[bid] for bid in matched_b)
    total_a = sum(norm_len_a[bid] for bid in pos_a)
    total_b = sum(norm_len_b[bid] for bid in pos_b)
    similarity = 0.0
    if total_a + total_b > 0:
        similarity = round((chars_a + chars_b) / (total_a + total_b), 4)
    return PairSimilarityResult(doc_a.docId, doc_b.docId, similarity, matches)
```

- [ ] **Step 3: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_align.py -q
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): difflib 块级单调对齐与 Dice 相似度计算"
```

---

## Task 10: 雷同簇聚类（Union-Find，≥3 份共同雷同）

**Files:**
- Create: `services/compare-algo/app/similarity/cluster.py`
- Create: `services/compare-algo/tests/test_cluster.py`

- [ ] **Step 1: 写失败测试**

`services/compare-algo/tests/test_cluster.py`：

```python
from app.similarity.align import PairSimilarityResult
from app.similarity.cluster import find_similarity_clusters


def _r(a: str, b: str, sim: float) -> PairSimilarityResult:
    return PairSimilarityResult(a, b, sim, [])


def test_cluster_of_4_via_transitive_merge():
    results = [_r("A", "B", 0.9), _r("B", "C", 0.8), _r("C", "D", 0.7)]
    assert find_similarity_clusters(results) == [["A", "B", "C", "D"]]


def test_pair_only_not_a_cluster():
    results = [_r("A", "B", 0.9)]
    assert find_similarity_clusters(results) == []


def test_below_threshold_not_merged():
    results = [_r("A", "B", 0.9), _r("B", "C", 0.3)]  # 0.3 < 默认 0.5
    assert find_similarity_clusters(results) == []


def test_two_independent_clusters():
    results = [_r("A", "B", 0.9), _r("B", "C", 0.9), _r("X", "Y", 0.8), _r("Y", "Z", 0.8)]
    clusters = find_similarity_clusters(results)
    assert sorted(clusters) == [["A", "B", "C"], ["X", "Y", "Z"]]


def test_custom_threshold():
    results = [_r("A", "B", 0.6), _r("B", "C", 0.6)]
    assert find_similarity_clusters(results, min_similarity=0.7) == []
    assert find_similarity_clusters(results, min_similarity=0.6) == [["A", "B", "C"]]
```

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_cluster.py -q
```

- [ ] **Step 2: 实现 `app/similarity/cluster.py`**

`services/compare-algo/app/similarity/cluster.py`：

```python
"""雷同簇聚类（spec §3.1）：两两高相似经 Union-Find 传递归并，≥3 份的簇单独标记。"""
from app.similarity.align import PairSimilarityResult


def find_similarity_clusters(
    pair_results: list[PairSimilarityResult],
    min_similarity: float = 0.5,
) -> list[list[str]]:
    """返回成员数 >= 3 的雷同簇（簇内 docId 升序）。不足 3 份的归并组不返回。"""
    parent: dict[str, str] = {}

    def find(x: str) -> str:
        parent.setdefault(x, x)
        while parent[x] != x:
            parent[x] = parent[parent[x]]  # 路径压缩
            x = parent[x]
        return x

    def union(a: str, b: str) -> None:
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    for r in pair_results:
        if r.similarity >= min_similarity:
            union(r.doc_id_a, r.doc_id_b)

    groups: dict[str, list[str]] = {}
    for doc_id in list(parent):
        groups.setdefault(find(doc_id), []).append(doc_id)
    return sorted(
        (sorted(members) for members in groups.values() if len(members) >= 3),
        key=lambda m: m[0],
    )
```

- [ ] **Step 3: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_cluster.py -q
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): Union-Find 雷同簇聚类（≥3 份共同雷同单独标记）"
```

---

## Task 11: similarity 证据组装 service（含 OCR 降权接入 + 真实样本对）

**Files:**
- Create: `services/compare-algo/app/similarity/service.py`
- Create: `services/compare-algo/tests/test_similarity_service.py`

证据规则：文档对 Dice ≥0.3 出证据；severity：≥0.8 high / ≥0.5 mid / 其余 low；命中低置信 OCR 块 → severity 降一级 + `metrics.ocrSuspect=true` + 文案标注（spec §4.5；low 触底不降时文案不声称「已降权」）；雷同簇（≥3 份）单独出证据（默认 high，命中低置信 OCR 块同样降一级），`metrics.cluster=true`，簇均值与定位只统计 ≥0.5 的归并边；证据标题使用 `meta.fileName`（缺失回退 docId），不暴露 opaque docId。

- [ ] **Step 1: 写失败测试**

`services/compare-algo/tests/test_similarity_service.py`：

```python
from app.angineer.adapter import adapt_document
from app.angineer.raw import validate_raw_document
from app.similarity.service import analyze_similarity
from tests.conftest import adapt, make_raw_block, make_raw_doc


def _adapt_all(raw_docs):
    return [adapt_document(validate_raw_document(d)) for d in raw_docs]


def test_fixture_pair_evidence(ir_docs):
    evidences = analyze_similarity("task-001", ir_docs)
    # fixture 中仅 doc-a/doc-b 雷同，doc-c 独立；不足 3 份，无簇证据
    assert len(evidences) == 1
    e = evidences[0]
    assert e.type == "similarity"
    assert e.taskId == "task-001"
    assert e.docIds == ["doc-a", "doc-b"]
    assert e.aiGenerated is False
    # Dice ≈ 0.87 → 本应 high，命中 doc-b b005 低置信 OCR 块 → 降为 mid（spec §4.5）
    assert e.metrics["similarity"] > 0.8
    assert e.metrics["ocrSuspect"] is True
    assert e.severity == "mid"
    assert "扫描件" in e.description
    # severity 实际降了一级（high→mid），文案须声称「已降权处理」
    assert "已降权处理" in e.description
    # 标题面向用户：使用 fileName 而非 opaque docId（通用约定）
    assert "A公司投标文件.pdf" in e.title
    assert "B公司投标文件.pdf" in e.title
    assert "doc-a" not in e.title


def test_evidence_locations_cover_matched_blocks(ir_docs):
    e = analyze_similarity("task-001", ir_docs)[0]
    loc_a = next(l for l in e.locations if l.docId == "doc-a")
    loc_b = next(l for l in e.locations if l.docId == "doc-b")
    assert "b003" in loc_a.blockIds
    assert "b003" in loc_b.blockIds
    assert "b005" in loc_b.blockIds  # OCR 块也定位出来


def test_no_evidence_for_independent_docs(ir_doc_a, ir_doc_c):
    assert analyze_similarity("task-001", [ir_doc_a, ir_doc_c]) == []


def test_cluster_evidence_for_3_similar_docs(ir_doc_a):
    # 克隆两份（改 docId），构成 3 份完全雷同 → 对证据 + 簇证据
    doc_b = ir_doc_a.model_copy(update={"docId": "doc-b"})
    doc_c = ir_doc_a.model_copy(update={"docId": "doc-c"})
    evidences = analyze_similarity("task-001", [ir_doc_a, doc_b, doc_c])
    pair_evidences = [e for e in evidences if not e.metrics.get("cluster")]
    cluster_evidences = [e for e in evidences if e.metrics.get("cluster")]
    assert len(pair_evidences) == 3  # 3 对，每对 similarity=1.0
    assert all(e.severity == "high" for e in pair_evidences)  # 无 OCR 块，不降权
    assert len(cluster_evidences) == 1
    ce = cluster_evidences[0]
    assert ce.type == "similarity"
    assert ce.severity == "high"
    assert ce.docIds == ["doc-a", "doc-b", "doc-c"]
    assert ce.metrics["memberCount"] == 3
    assert ce.metrics["avgSimilarity"] == 1.0
    assert ce.metrics["ocrSuspect"] is False
    assert len(ce.locations) == 3
    # 簇标题同样使用 fileName
    assert "A公司投标文件.pdf" in ce.title
    assert "doc-a" not in ce.title


def test_similarity_thresholds(ir_doc_a):
    # 相似度 < 0.3 不出证据：只保留一个极小重合块无法构造（候选 Jaccard 0.5 已过滤），
    # 这里验证阈值常量存在且单调
    from app.similarity import service
    assert service.SEVERITY_HIGH > service.SEVERITY_MID > service.EVIDENCE_MIN_SIMILARITY > 0


def test_real_haigang_pair_low_evidence(raw_haigang_pair):
    """真实部分雷同对（海港1 vs 海港2）：实测 Dice≈0.346 → 恰好一条 low 证据。

    实测值（2026-08-08 按本计划算法离线演算）：34 个候选块对全部通过单调对齐，
    Dice = 0.3460（≥0.3 出证据，<0.5 为 low）。MinHash/LSH 为近似召回，
    边界块对可能有少量出入，故相似度断言给区间而非精确值。
    """
    docs = _adapt_all(raw_haigang_pair)
    evidences = analyze_similarity("task-real", docs)
    pair_evidences = [e for e in evidences if not e.metrics.get("cluster")]
    assert len(pair_evidences) == 1
    e = pair_evidences[0]
    assert e.docIds == ["doc-12f45ca9", "doc-c8be9f8b"]
    assert 0.3 <= e.metrics["similarity"] < 0.5
    assert e.metrics["similarity"] == 0.3469  # 当前实现实测固化值（区间内的精确钉）
    assert e.severity == "low"
    assert e.metrics["ocrSuspect"] is False  # 真实数据 confidence 全 1.0，不降权
    assert e.metrics["matchedBlockCount"] >= 20
    # 标题使用真实 fileName（海港1.pdf / 海港2.pdf），不暴露 opaque docId
    assert "海港1.pdf" in e.title
    assert "海港2.pdf" in e.title
    assert "doc-12f45ca9" not in e.title


def test_real_pingshen_pair_identical(raw_pingshen_pair):
    """评审办法副本对（内容完全一致）：Dice=1.0 → high 证据。"""
    docs = _adapt_all(raw_pingshen_pair)
    evidences = analyze_similarity("task-real", docs)
    assert len(evidences) == 1
    e = evidences[0]
    assert e.metrics["similarity"] == 1.0
    assert e.severity == "high"


def test_analyze_similarity_deterministic(raw_haigang_pair):
    """同一输入两次运行产出完全一致（除自动生成的 id）——LSH/排序无隐式随机性。"""
    docs = _adapt_all(raw_haigang_pair)
    first = analyze_similarity("task-real", docs)
    second = analyze_similarity("task-real", docs)
    assert [e.model_dump(exclude={"id"}) for e in first] == [
        e.model_dump(exclude={"id"}) for e in second
    ]


def test_title_falls_back_to_doc_id_when_filename_missing():
    para = "本工程采用框架剪力墙结构体系抗震设防烈度为七度"
    raw_x = make_raw_doc("doc-x", file_name=None, author=None, created_at=None,
                         blocks=[make_raw_block("x-b01", para)])
    raw_y = make_raw_doc("doc-y", file_name=None, author=None, created_at=None,
                         blocks=[make_raw_block("y-b01", para)])
    e = analyze_similarity("task-fallback", [adapt(raw_x), adapt(raw_y)])[0]
    assert e.title == "doc-x 与 doc-y 存在文本雷同（相似度 100.0%）"


# --- 链式簇场景：A-B / B-C 为 ≥0.5 归并边，A-C 仅共享一块（弱边 <0.5，不稀释簇） ---
_S = [
    "本工程采用框架剪力墙结构体系抗震设防烈度为七度",
    "施工组织设计包含进度计划资源配置与安全保证措施",
    "模板工程选用覆膜木胶合板支撑体系采用盘扣式脚手架",
    "混凝土浇筑采用商品混凝土泵送入模分层振捣密实",
    "施工现场临时用电执行三级配电两级保护标准",
]
_T = [
    "质量保证体系覆盖原材料进场检验试验与工序交接检查验收",
    "雨季施工安排包括基坑抽排水材料防雨防潮与边坡防护措施",
    "塔吊选型满足最大构件吊装重量与最大作业半径覆盖要求",
    "文明施工承诺包含围挡全封闭扬尘在线控制与噪声管理",
    "竣工验收资料按城建档案馆归档要求同步整理组卷移交",
]
_W_CHAIN = "本承诺书有效期与投标有效期一致"
_UA = [
    "我司近年完成同类市政道路工程业绩六项",
    "项目班子配备一级建造师两名与技术负责人一名",
    "拟投入履带挖掘机两台压路机三台摊铺机一台",
    "农民工工资按月足额发放",
]
_UC = [
    "智慧工地平台实现人员定位与视频监控全覆盖",
    "绿色施工执行四节一环保评价标准各项指标",
    "深基坑支护采用排桩加内支撑联合支护形式",
    "bim技术应用于管线综合排布与碰撞检查",
]


def _make_doc(doc_id: str, file_name: str, texts: list[str]):
    return make_raw_doc(
        doc_id,
        file_name=file_name,
        author=None,
        created_at=None,
        blocks=[make_raw_block(f"{doc_id}-b{i:02d}", text, seq=i)
                for i, text in enumerate(texts, 1)],
    )


def test_cluster_avg_and_locations_over_union_edges_only():
    # A 共享 S 给 B，C 共享 T 给 B；A-C 仅共享一块 W（弱边）
    docs = [
        adapt(_make_doc("doc-a", "A.pdf", _S + [_W_CHAIN] + _UA)),
        adapt(_make_doc("doc-b", "B.pdf", _S + _T)),
        adapt(_make_doc("doc-c", "C.pdf", _T + [_W_CHAIN] + _UC)),
    ]
    evidences = analyze_similarity("task-chain", docs)
    pair_evidences = [e for e in evidences if not e.metrics.get("cluster")]
    cluster_evidences = [e for e in evidences if e.metrics.get("cluster")]
    # A-C 弱边（<0.3）不出对证据
    assert sorted(e.docIds for e in pair_evidences) == [["doc-a", "doc-b"], ["doc-b", "doc-c"]]
    assert len(cluster_evidences) == 1
    ce = cluster_evidences[0]
    assert ce.docIds == ["doc-a", "doc-b", "doc-c"]
    sims = {tuple(e.docIds): e.metrics["similarity"] for e in pair_evidences}
    assert sims[("doc-a", "doc-b")] >= 0.5
    assert sims[("doc-b", "doc-c")] >= 0.5
    # 均值只取构成归并的边（A-B、B-C），不含 A-C 弱边
    expected = round((sims[("doc-a", "doc-b")] + sims[("doc-b", "doc-c")]) / 2, 4)
    assert ce.metrics["avgSimilarity"] == expected
    # 弱边块不进入簇定位
    loc_a = next(l for l in ce.locations if l.docId == "doc-a")
    loc_c = next(l for l in ce.locations if l.docId == "doc-c")
    assert loc_a.blockIds == [f"doc-a-b0{i}" for i in range(1, 6)]
    assert loc_c.blockIds == [f"doc-c-b0{i}" for i in range(1, 6)]
    assert "doc-a-b06" not in loc_a.blockIds  # W 块只属于 A-C 弱边
    assert "doc-c-b06" not in loc_c.blockIds


def test_cluster_ocr_downgrade(ir_doc_b):
    # 3 份完全雷同且匹配块命中低置信 OCR → 对证据与簇证据都降一级
    docs = [ir_doc_b] + [
        ir_doc_b.model_copy(update={"docId": d}) for d in ("doc-b2", "doc-b3")
    ]
    evidences = analyze_similarity("task-ocr", docs)
    pair_evidences = [e for e in evidences if not e.metrics.get("cluster")]
    cluster_evidences = [e for e in evidences if e.metrics.get("cluster")]
    assert len(pair_evidences) == 3
    assert all(e.metrics["similarity"] == 1.0 for e in pair_evidences)
    assert all(e.severity == "mid" for e in pair_evidences)  # high 降一级
    assert all(e.metrics["ocrSuspect"] is True for e in pair_evidences)
    assert len(cluster_evidences) == 1
    ce = cluster_evidences[0]
    assert ce.docIds == ["doc-b", "doc-b2", "doc-b3"]
    assert ce.severity == "mid"  # 簇证据同样降权，不再硬编码 high
    assert ce.metrics["ocrSuspect"] is True
    assert ce.metrics["memberCount"] == 3
    assert ce.metrics["avgSimilarity"] == 1.0
    assert "扫描件" in ce.description
    assert "已降权处理" in ce.description
    assert len(ce.locations) == 3


# --- low 触底场景：OCR 降权为 no-op，文案不得声称「已降权」 ---
_W_LONG = (
    "若我方中标将在合同签订后三十个日历日内组织人员机械进场施工"
    "严格按照招标文件要求的质量标准与安全规范组织实施"
    "并按规定缴纳履约保证金确保工程按期保质保量完成特此郑重承诺"
)
_UX = [
    "我司具备市政公用工程施工总承包一级资质近五年完成同类道路桥梁工程业绩十余项履约信誉良好",
    "本项目拟投入履带式挖掘机四台振动压路机六台沥青摊铺机两台均经检测合格配备专职操作人员",
    "项目部实行工程款专户专用管理制度农民工工资按月足额代发设立维权公示牌接受社会监督",
]
_UY = [
    "智慧工地管理平台实现人员实名制定位视频远程监控扬尘噪声在线监测与塔吊黑匣子数据覆盖",
    "绿色施工严格执行四节一环保评价标准建筑垃圾分类收集清运现场裸土全覆盖自动喷淋降尘",
    "深基坑工程采用排桩加预应力锚索联合支护形式开挖过程实施信息化监测变形数据日报送监理",
]


def test_low_severity_ocr_note_does_not_claim_downgrade():
    # 唯一匹配块是低置信 OCR 块，相似度落在 [0.3, 0.5) → low 触底，降权为 no-op
    raw_x = _make_doc("doc-x", "X.pdf", [_W_LONG] + _UX)
    raw_x["blocks"][0]["source"] = "ocr"
    raw_x["blocks"][0]["confidence"] = 0.3
    raw_y = _make_doc("doc-y", "Y.pdf", [_W_LONG] + _UY)
    evidences = analyze_similarity("task-low", [adapt(raw_x), adapt(raw_y)])
    assert len(evidences) == 1
    e = evidences[0]
    assert 0.3 <= e.metrics["similarity"] < 0.5
    assert e.severity == "low"  # low 触底不再降
    assert e.metrics["ocrSuspect"] is True
    assert "扫描件" in e.description  # 仍标注 OCR 风险
    assert "已降权处理" not in e.description  # 但未实际降权，不谎称
```

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_similarity_service.py -q
```

- [ ] **Step 2: 实现 `app/similarity/service.py`**

`services/compare-algo/app/similarity/service.py`：

```python
"""查重证据组装：similarity 类证据，aiGenerated=false。"""
from app.ocr import downgrade_severity, low_confidence_ocr_block_ids
from app.schemas.evidence import Evidence, EvidenceLocation, Severity, build_evidence
from app.schemas.ir import IrDocument
from app.similarity.align import PairSimilarityResult, align_document_pair
from app.similarity.cluster import find_similarity_clusters
from app.similarity.minhash import CandidatePair, build_block_index, find_candidate_pairs

EVIDENCE_MIN_SIMILARITY = 0.3   # 低于该值不出证据
SEVERITY_HIGH = 0.8
SEVERITY_MID = 0.5
CLUSTER_MIN_SIMILARITY = 0.5    # 簇归并阈值


def _severity_of(similarity: float) -> Severity:
    if similarity >= SEVERITY_HIGH:
        return "high"
    if similarity >= SEVERITY_MID:
        return "mid"
    return "low"


def _display_names(documents: list[IrDocument]) -> dict[str, str]:
    """面向用户的文档标识：优先 fileName，缺失/空串时回退 docId（通用约定）。"""
    return {d.docId: (d.meta.fileName or d.docId) for d in documents}


def _is_ocr_suspect(r: PairSimilarityResult, ocr_ids: dict[str, set[str]]) -> bool:
    """任一匹配块命中低置信 OCR 块 → 证据须降权（spec §4.5）。"""
    return any(
        m.block_id_a in ocr_ids[r.doc_id_a] or m.block_id_b in ocr_ids[r.doc_id_b]
        for m in r.matches
    )


def _apply_ocr_downgrade(severity: Severity, suspect: bool) -> tuple[Severity, str]:
    """OCR 降权 + 文案标注；low 触底 severity 不变时，文案不声称「已降权」。"""
    if not suspect:
        return severity, ""
    downgraded = downgrade_severity(severity)
    note = "部分雷同块来自扫描件 OCR 低置信识别，准确率可能受影响。"
    if downgraded != severity:
        note += "已降权处理。"
    return downgraded, note


def _pair_evidence(
    task_id: str,
    r: PairSimilarityResult,
    suspect: bool,
    names: dict[str, str],
) -> Evidence:
    severity, ocr_note = _apply_ocr_downgrade(_severity_of(r.similarity), suspect)
    avg_jac = round(sum(m.jaccard for m in r.matches) / len(r.matches), 4)
    return build_evidence(
        task_id=task_id,
        type="similarity",
        severity=severity,
        doc_ids=[r.doc_id_a, r.doc_id_b],
        locations=[
            EvidenceLocation(docId=r.doc_id_a, blockIds=sorted({m.block_id_a for m in r.matches})),
            EvidenceLocation(docId=r.doc_id_b, blockIds=sorted({m.block_id_b for m in r.matches})),
        ],
        metrics={
            "similarity": r.similarity,
            "avgBlockJaccard": avg_jac,
            "matchedBlockCount": len(r.matches),
            "ocrSuspect": suspect,
        },
        title=f"{names[r.doc_id_a]} 与 {names[r.doc_id_b]} 存在文本雷同（相似度 {r.similarity:.1%}）",
        description="两份文档存在大段雷同。" + ocr_note,
    )


def analyze_similarity(task_id: str, documents: list[IrDocument]) -> list[Evidence]:
    index = build_block_index(documents)
    pairs = find_candidate_pairs(index)
    by_doc_pair: dict[tuple[str, str], list[CandidatePair]] = {}
    for p in pairs:
        by_doc_pair.setdefault((p.doc_id_a, p.doc_id_b), []).append(p)

    doc_map = {d.docId: d for d in documents}
    ocr_ids = {d.docId: low_confidence_ocr_block_ids(d) for d in documents}
    names = _display_names(documents)

    results: list[PairSimilarityResult] = []
    for (a, b), group in sorted(by_doc_pair.items()):
        results.append(align_document_pair(doc_map[a], doc_map[b], group))

    evidences: list[Evidence] = []
    for r in results:
        if r.similarity < EVIDENCE_MIN_SIMILARITY or not r.matches:
            continue
        evidences.append(_pair_evidence(task_id, r, _is_ocr_suspect(r, ocr_ids), names))

    for members in find_similarity_clusters(results, CLUSTER_MIN_SIMILARITY):
        # 只统计构成归并的边（≥簇阈值）：弱边/非证据对不稀释均值、不进入定位
        sub = [
            r for r in results
            if r.doc_id_a in members
            and r.doc_id_b in members
            and r.similarity >= CLUSTER_MIN_SIMILARITY
            and r.matches
        ]
        avg_sim = round(sum(r.similarity for r in sub) / len(sub), 4)
        suspect = any(_is_ocr_suspect(r, ocr_ids) for r in sub)
        severity, ocr_note = _apply_ocr_downgrade("high", suspect)
        locations: list[EvidenceLocation] = []
        for m in members:
            block_ids = sorted(
                {mm.block_id_a for r in sub if r.doc_id_a == m for mm in r.matches}
                | {mm.block_id_b for r in sub if r.doc_id_b == m for mm in r.matches}
            )
            if block_ids:
                locations.append(EvidenceLocation(docId=m, blockIds=block_ids))
        evidences.append(build_evidence(
            task_id=task_id,
            type="similarity",
            severity=severity,
            doc_ids=members,
            locations=locations,
            metrics={
                "cluster": True,
                "memberCount": len(members),
                "avgSimilarity": avg_sim,
                "ocrSuspect": suspect,
            },
            title=f"{len(members)} 份标书存在共同雷同（{', '.join(names[m] for m in members)}）",
            description="≥3 份标书经两两高相似传递归并构成雷同簇，是围串标强信号。" + ocr_note,
        ))
    return evidences
```

- [ ] **Step 3: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_similarity_service.py -q
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): similarity 证据组装 service（OCR 降权 + 雷同簇 + 真实样本断言）"
```

## Task 12: 报价表格解析与数值归一

**Files:**
- Create: `services/compare-algo/app/pricing/number_norm.py`
- Create: `services/compare-algo/app/pricing/table_parse.py`
- Create: `services/compare-algo/tests/test_pricing_parse.py`

- [ ] **Step 1: 写失败测试**

`services/compare-algo/tests/test_pricing_parse.py`：

```python
import pytest

from app.pricing.number_norm import parse_amount
from app.pricing.table_parse import extract_amounts, extract_total_amount, parse_table_html


class TestParseAmount:
    def test_plain_number(self):
        assert parse_amount("1000000") == 1000000.0

    def test_thousands_separator(self):
        assert parse_amount("1,000,000.00") == 1000000.0

    def test_currency_symbol(self):
        assert parse_amount("￥1,000,000.00") == 1000000.0
        assert parse_amount("¥500") == 500.0

    def test_wan_unit_normalized_to_yuan(self):
        assert parse_amount("100万元") == 1000000.0
        assert parse_amount("100万") == 1000000.0
        assert parse_amount("3.5万元") == 35000.0

    def test_wan_float_artifact_rounded(self):
        # 9876.54 * 10000 浮点伪影须归一到分（2 位小数）
        assert parse_amount("9876.54万") == 98765400.0

    def test_embedded_in_label(self):
        assert parse_amount("小写：1,000,000.00 元") == 1000000.0

    def test_unparseable_returns_none(self):
        assert parse_amount("") is None
        assert parse_amount("无报价") is None
        assert parse_amount(None) is None


class TestParseTableHtml:
    def test_simple_grid(self):
        grid = parse_table_html("<table><tr><td>a</td><td>b</td></tr><tr><td>c</td><td>d</td></tr></table>")
        assert grid == [["a", "b"], ["c", "d"]]

    def test_rowspan_expanded_with_same_value(self):
        grid = parse_table_html(
            '<table><tr><td rowspan="2">a</td><td>b</td></tr><tr><td>c</td></tr></table>'
        )
        assert grid == [["a", "b"], ["a", "c"]]

    def test_colspan_expanded(self):
        grid = parse_table_html(
            '<table><tr><td colspan="2">a</td></tr><tr><td>b</td><td>c</td></tr></table>'
        )
        assert grid == [["a", "a"], ["b", "c"]]

    def test_rowspan_plus_colspan(self):
        grid = parse_table_html(
            '<table><tr><td rowspan="2" colspan="2">x</td><td>b</td></tr><tr><td>c</td></tr></table>'
        )
        assert grid == [["x", "x", "b"], ["x", "x", "c"]]

    def test_no_table_tag_raises(self):
        with pytest.raises(ValueError):
            parse_table_html("<div>not a table</div>")


class TestExtractTotalAmount:
    def test_keyword_row_preferred(self):
        grid = [
            ["项目", "金额"],
            ["分部分项工程费", "800,000.00"],
            ["投标总价（元）", "1,000,000.00"],
        ]
        assert extract_total_amount(grid) == 1000000.0

    def test_fallback_to_max_amount(self):
        grid = [["单价", "100"], ["数量", "5"], ["小计", "500"]]
        assert extract_total_amount(grid) == 500.0

    def test_no_amounts_returns_none(self):
        assert extract_total_amount([["项目", "说明"], ["工期", "一年"]]) is None

    def test_fallback_ignores_compact_date(self):
        # 无关键词行时，紧凑型日期（8 位裸数字，如 20251229）不得作为总价候选
        grid = [["日期", "20251229"], ["金额", "500000"]]
        assert extract_total_amount(grid) == 500000.0

    def test_fallback_only_compact_date_returns_none(self):
        assert extract_total_amount([["日期", "20251229"]]) is None

    def test_extract_amounts_collects_all(self):
        grid = [["a", "100"], ["200", "c"]]
        assert extract_amounts(grid) == [100.0, 200.0]


def test_fixture_price_tables(ir_doc_a):
    table_block = next(b for b in ir_doc_a.blocks if b.type == "table")
    grid = parse_table_html(table_block.table.html)
    assert extract_total_amount(grid) == 1000000.0


def test_real_fixture_table_parse(raw_haigang_pair):
    """真实表格 html 可解析（海港2 含多张纯净表格）。"""
    from app.angineer.adapter import adapt_document
    from app.angineer.raw import validate_raw_document
    doc = adapt_document(validate_raw_document(raw_haigang_pair[1]))
    tables = [b for b in doc.blocks if b.type == "table" and b.table and b.table.html]
    assert len(tables) > 0
    for t in tables:
        grid = parse_table_html(t.table.html)
        assert all(isinstance(row, list) for row in grid)
```

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_pricing_parse.py -q
```

- [ ] **Step 2: 实现 `app/pricing/number_norm.py`**

`services/compare-algo/app/pricing/number_norm.py`：

```python
"""报价数值解析与单位归一：千分位、货币符号、「万元/万」单位统一归到「元」。"""
import re

_AMOUNT_RE = re.compile(r"-?\d[\d,]*(?:\.\d+)?")


def parse_amount(raw: str | None) -> float | None:
    """从单元格文本解析金额（单位：元）。无法解析返回 None。

    已知局限：含「万」的非金额文本（如「10万平方米」）也会被 ×10000，
    报价表场景可接受，后续如出现误判再引入列语义判断。
    """
    if raw is None:
        return None
    text = raw.strip()
    if not text:
        return None
    multiplier = 10000.0 if "万" in text else 1.0
    m = _AMOUNT_RE.search(text.replace("￥", "").replace("¥", ""))
    if not m:
        return None
    try:
        value = float(m.group(0).replace(",", ""))
    except ValueError:
        return None
    # round 到分：消除「万」换算浮点伪影（9876.54万 → 98765400.00000001）
    return round(value * multiplier, 2)
```

- [ ] **Step 3: 实现 `app/pricing/table_parse.py`**

`services/compare-algo/app/pricing/table_parse.py`：

```python
"""表格 html 解析：rowspan/colspan 展开为完整网格（占位格填同一值）。

输入 html 已通过产物 schema 纯净度校验（仅 table/tr/td/th），此处不做安全过滤。
"""
import re

from bs4 import BeautifulSoup

from app.pricing.number_norm import parse_amount

_TOTAL_KEYWORDS = ("总价", "合计", "投标报价", "总报价", "总金额")
# 紧凑型日期（8 位裸数字，如 20251229）：fallback max() 须排除，否则会压过真实报价
_COMPACT_DATE_RE = re.compile(r"^(19|20)\d{6}$")


def parse_table_html(html: str) -> list[list[str]]:
    soup = BeautifulSoup(html, "lxml")
    table = soup.find("table")
    if table is None:
        raise ValueError("table.html 中没有 <table> 标签")
    grid: list[list[str]] = []
    spans: dict[tuple[int, int], str] = {}  # (row, col) -> 被上方 rowspan 占位的值
    for r, tr in enumerate(table.find_all("tr")):
        row: list[str] = []
        col = 0
        for cell in tr.find_all(["td", "th"]):
            while (r, col) in spans:
                row.append(spans.pop((r, col)))
                col += 1
            text = cell.get_text(strip=True)
            rowspan = int(cell.get("rowspan", 1))
            colspan = int(cell.get("colspan", 1))
            for dc in range(colspan):
                row.append(text)
                for dr in range(1, rowspan):
                    spans[(r + dr, col + dc)] = text
            col += colspan
        while (r, col) in spans:  # 行尾被上方 rowspan 占满的格
            row.append(spans.pop((r, col)))
            col += 1
        grid.append(row)
    return grid


def extract_amounts(grid: list[list[str]]) -> list[float]:
    amounts: list[float] = []
    for row in grid:
        for cell in row:
            v = parse_amount(cell)
            if v is not None:
                amounts.append(v)
    return amounts


def extract_total_amount(grid: list[list[str]]) -> float | None:
    """优先取含 总价/合计/报价 关键词行中的最大金额；否则取全表最大金额。

    fallback max() 排除紧凑型日期（如 20251229），避免 8 位裸数字虚高总价。
    """
    keyword_amounts: list[float] = []
    for row in grid:
        if any(k in "".join(row) for k in _TOTAL_KEYWORDS):
            for cell in row:
                v = parse_amount(cell)
                if v is not None:
                    keyword_amounts.append(v)
    if keyword_amounts:
        return max(keyword_amounts)
    amounts = [
        v for row in grid for cell in row
        if not _COMPACT_DATE_RE.match(cell.strip())
        and (v := parse_amount(cell)) is not None
    ]
    return max(amounts) if amounts else None
```

- [ ] **Step 4: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_pricing_parse.py -q
```

- [ ] **Step 5: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): 报价表格 html 解析与金额单位归一（千分位/万元）"
```

---

## Task 13: 报价规律检测与 pricing 证据组装

**Files:**
- Create: `services/compare-algo/app/pricing/patterns.py`
- Create: `services/compare-algo/app/pricing/service.py`
- Create: `services/compare-algo/tests/test_pricing_patterns.py`
- Create: `services/compare-algo/tests/test_pricing_service.py`

检测规则（tech 决策：等差、尾数规律、贴近度）：
- 等差：≥3 份，升序相邻差值的相对偏差 ≤1% → high；
- 尾数：≥3 份，整数部分末两位完全相同 → mid（全 0 尾如 "00" 属百元取整常态，排除以免误报）；
- 贴近度：≥2 份，(max-min)/max ≤1% → high（≤0.5% 时）/ mid。

- [ ] **Step 1: 写失败测试**

`services/compare-algo/tests/test_pricing_patterns.py`：

```python
from app.pricing.patterns import (
    detect_arithmetic_progression,
    detect_closeness,
    detect_tail_pattern,
)


class TestArithmeticProgression:
    def test_exact_ap(self):
        r = detect_arithmetic_progression([1000000, 1020000, 1010000])  # 乱序输入
        assert r is not None
        assert r.amounts == [1000000, 1010000, 1020000]
        assert r.common_diff == 10000

    def test_near_ap_within_tolerance(self):
        assert detect_arithmetic_progression([100, 200, 301]) is not None  # 差 100/101

    def test_not_ap(self):
        assert detect_arithmetic_progression([100, 200, 320]) is None

    def test_two_amounts_not_enough(self):
        assert detect_arithmetic_progression([100, 200]) is None

    def test_equal_amounts_not_ap(self):
        assert detect_arithmetic_progression([100, 100, 100]) is None


class TestTailPattern:
    def test_same_tail(self):
        r = detect_tail_pattern([10067, 20067, 30067])
        assert r is not None
        assert r.tail == "67"

    def test_trivial_zero_tail_excluded(self):
        # 末两位 "00" 是百元取整常态，不构成尾数规律
        assert detect_tail_pattern([10000, 20000, 30000]) is None

    def test_different_tails(self):
        assert detect_tail_pattern([10001, 20002, 30003]) is None

    def test_two_amounts_not_enough(self):
        assert detect_tail_pattern([10000, 20000]) is None


class TestCloseness:
    def test_close(self):
        r = detect_closeness([1000, 1005])
        assert r is not None
        assert r.spread_ratio == round(5 / 1005, 6)

    def test_not_close(self):
        assert detect_closeness([1000, 1100]) is None

    def test_single_amount_not_enough(self):
        assert detect_closeness([1000]) is None
```

`services/compare-algo/tests/test_pricing_service.py`：

```python
from app.pricing.service import analyze_pricing


def test_fixture_arithmetic_evidence(ir_docs):
    evidences = analyze_pricing("task-001", ir_docs)
    # 1,000,000 / 1,010,000 / 1,020,000：等差（公差 10,000），
    # 贴近度 20000/1020000≈1.96% > 1% 不触发，尾数全为 "00"（百元取整常态）排除不触发
    assert len(evidences) == 1
    e = evidences[0]
    assert e.type == "pricing"
    assert e.severity == "high"
    assert e.aiGenerated is False
    assert e.metrics["pattern"] == "arithmetic"
    assert e.metrics["commonDiff"] == 10000
    assert e.metrics["amounts"] == {"doc-a": 1000000.0, "doc-b": 1010000.0, "doc-c": 1020000.0}
    assert e.docIds == ["doc-a", "doc-b", "doc-c"]


def test_pricing_locations_point_to_table_blocks(ir_docs):
    e = analyze_pricing("task-001", ir_docs)[0]
    loc = {l.docId: l.blockIds for l in e.locations}
    assert loc == {"doc-a": ["b005"], "doc-b": ["b004"], "doc-c": ["b004"]}


def test_less_than_two_priced_docs_no_evidence(ir_doc_a):
    # 单份文档无法比报价
    assert analyze_pricing("task-001", [ir_doc_a]) == []
    # 无表格的文档不参与
    no_table = ir_doc_a.model_copy(update={
        "docId": "doc-x",
        "blocks": [b for b in ir_doc_a.blocks if b.type != "table"],
    })
    assert analyze_pricing("task-001", [ir_doc_a, no_table]) == []


def test_table_without_html_skipped(ir_doc_a):
    # 实测 2/132 表格无 table_html（有截图）：无法解析金额，跳过不参与报价比对
    no_html = ir_doc_a.model_copy(update={
        "docId": "doc-nh",
        "blocks": [
            b.model_copy(update={"table": b.table.model_copy(update={"html": None})})
            if b.type == "table" else b
            for b in ir_doc_a.blocks
        ],
    })
    assert analyze_pricing("task-001", [ir_doc_a, no_html]) == []


def test_malformed_span_table_skipped(ir_doc_a, ir_doc_b):
    """畸形 rowspan="abc" 的表格解析抛 ValueError → 跳过该表，不拖垮整个请求。"""
    bad = ir_doc_a.model_copy(update={
        "docId": "doc-bad",
        "blocks": [
            b.model_copy(update={"table": b.table.model_copy(update={
                "html": '<table><tr><td rowspan="abc">总价</td><td>9,999,999.00</td></tr></table>'
            })})
            if b.type == "table" else b
            for b in ir_doc_a.blocks
        ],
    })
    # bad 的唯一报价表畸形 → 不参与；doc-a/doc-b 正常比对（贴近度 0.99% ≤ 1% 出证据）
    evidences = analyze_pricing("task-001", [bad, ir_doc_a, ir_doc_b])
    assert evidences
    assert all("doc-bad" not in e.docIds for e in evidences)


def test_real_haigang_pair_no_pricing_evidence(raw_haigang_pair):
    """海港1 无表格 → 可报价文档不足 2 份，不出证据（真实数据负路径）。"""
    from app.angineer.adapter import adapt_document
    from app.angineer.raw import validate_raw_document
    docs = [adapt_document(validate_raw_document(d)) for d in raw_haigang_pair]
    assert analyze_pricing("task-real", docs) == []
```

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_pricing_patterns.py tests/test_pricing_service.py -q
```

- [ ] **Step 2: 实现 `app/pricing/patterns.py`**

`services/compare-algo/app/pricing/patterns.py`：

```python
"""报价规律检测：等差 / 尾数 / 贴近度。全部纯函数，输入为各文档投标总价。"""
from dataclasses import dataclass


@dataclass
class ArithmeticProgression:
    amounts: list[float]   # 升序
    common_diff: float
    max_deviation: float


def detect_arithmetic_progression(
    amounts: list[float], rel_tol: float = 0.01
) -> ArithmeticProgression | None:
    """≥3 份报价构成等差数列：相邻差值相对平均公差的偏差 ≤ rel_tol。"""
    if len(amounts) < 3:
        return None
    s = sorted(amounts)
    diffs = [s[i + 1] - s[i] for i in range(len(s) - 1)]
    if any(d <= 0 for d in diffs):
        return None
    avg = sum(diffs) / len(diffs)
    max_dev = max(abs(d - avg) for d in diffs)
    if max_dev / avg <= rel_tol:
        return ArithmeticProgression(amounts=s, common_diff=round(avg, 2), max_deviation=round(max_dev, 2))
    return None


@dataclass
class TailPattern:
    tail: str
    amounts: list[float]   # 升序


def detect_tail_pattern(amounts: list[float], tail_len: int = 2) -> TailPattern | None:
    """≥3 份报价整数部分末 tail_len 位完全相同（尾数规律，疑似同源编制）。

    末位全 0（如 "00"，百元取整）是报价常态而非规律，排除以免误报。
    """
    if len(amounts) < 3:
        return None
    tails = [str(int(a)).zfill(tail_len)[-tail_len:] for a in amounts]
    if len(set(tails)) == 1 and tails[0] != "0" * tail_len:
        return TailPattern(tail=tails[0], amounts=sorted(amounts))
    return None


@dataclass
class Closeness:
    min_amount: float
    max_amount: float
    spread_ratio: float


def detect_closeness(amounts: list[float], max_spread: float = 0.01) -> Closeness | None:
    """(max-min)/max ≤ max_spread（默认 1%）视为异常贴近。"""
    if len(amounts) < 2:
        return None
    lo, hi = min(amounts), max(amounts)
    if hi <= 0:
        return None
    spread = (hi - lo) / hi
    if spread <= max_spread:
        return Closeness(min_amount=lo, max_amount=hi, spread_ratio=round(spread, 6))
    return None
```

- [ ] **Step 3: 实现 `app/pricing/service.py`**

`services/compare-algo/app/pricing/service.py`：

```python
"""报价分析证据组装：pricing 类证据，aiGenerated=false。"""
from app.pricing.patterns import (
    detect_arithmetic_progression,
    detect_closeness,
    detect_tail_pattern,
)
from app.pricing.table_parse import extract_total_amount, parse_table_html
from app.schemas.evidence import Evidence, EvidenceLocation, build_evidence
from app.schemas.ir import IrDocument


def _best_price_block(doc: IrDocument) -> tuple[str, float] | None:
    """取文档中投标总价最大的表格块，返回 (blockId, total)。

    无表格、表格无 html（实测 2/132，仅有截图）或无金额时返回 None；
    畸形 html（如 rowspan="abc"）解析抛 ValueError，跳过该表不拖垮整个请求。
    """
    best: tuple[str, float] | None = None
    for block in doc.blocks:
        if block.type != "table" or block.table is None or block.table.html is None:
            continue
        try:
            grid = parse_table_html(block.table.html)
        except ValueError:
            continue
        total = extract_total_amount(grid)
        if total is not None and (best is None or total > best[1]):
            best = (block.blockId, total)
    return best


def analyze_pricing(task_id: str, documents: list[IrDocument]) -> list[Evidence]:
    priced = [(d.docId, bp) for d in documents if (bp := _best_price_block(d)) is not None]
    if len(priced) < 2:
        return []
    doc_ids = [doc_id for doc_id, _ in priced]
    amounts = [bp[1] for _, bp in priced]
    amount_map = {doc_id: bp[1] for doc_id, bp in priced}
    locations = [EvidenceLocation(docId=doc_id, blockIds=[bp[0]]) for doc_id, bp in priced]

    evidences: list[Evidence] = []
    ap = detect_arithmetic_progression(amounts)
    if ap is not None:
        evidences.append(build_evidence(
            task_id=task_id,
            type="pricing",
            severity="high",
            doc_ids=doc_ids,
            locations=locations,
            metrics={"pattern": "arithmetic", "commonDiff": ap.common_diff, "amounts": amount_map},
            title=f"{len(doc_ids)} 份报价呈等差规律（公差约 {ap.common_diff:,.0f} 元）",
            description="多份投标报价构成等差数列，疑似人为排布，属围串标强信号。",
        ))
    tail = detect_tail_pattern(amounts)
    if tail is not None:
        evidences.append(build_evidence(
            task_id=task_id,
            type="pricing",
            severity="mid",
            doc_ids=doc_ids,
            locations=locations,
            metrics={"pattern": "tail", "tail": tail.tail, "amounts": amount_map},
            title=f"{len(doc_ids)} 份报价尾数完全相同（末两位 {tail.tail}）",
            description="多份报价尾数规律一致，疑似同源编制。",
        ))
    close = detect_closeness(amounts)
    if close is not None:
        evidences.append(build_evidence(
            task_id=task_id,
            type="pricing",
            severity="high" if close.spread_ratio <= 0.005 else "mid",
            doc_ids=doc_ids,
            locations=locations,
            metrics={
                "pattern": "closeness",
                "spreadRatio": close.spread_ratio,
                "minAmount": close.min_amount,
                "maxAmount": close.max_amount,
                "amounts": amount_map,
            },
            title=f"{len(doc_ids)} 份报价异常贴近（最大偏差 {close.spread_ratio:.2%}）",
            description="多份报价贴近度异常，疑似协同报价。",
        ))
    return evidences
```

- [ ] **Step 4: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_pricing_patterns.py tests/test_pricing_service.py -q
```

- [ ] **Step 5: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): 报价规律检测（等差/尾数/贴近度）与 pricing 证据组装"
```

## Task 14: 元数据比对（author / createdAt / creatorTool，含 PDF 日期归一集成）

**Files:**
- Create: `services/compare-algo/app/metadata/service.py`（本 Task 先实现 `compare_meta_fields`，Task 15 追加 `detect_shared_typos` 与 `analyze_metadata`）
- Create: `services/compare-algo/tests/test_metadata.py`

规则：author 相同（非 null，≥2 份）→ mid；createdAt 完全相同（非 null，≥2 份，适配层已归一 ISO）→ mid；creatorTool 全部文档相同 → low（常见工具相同仅作弱线索）。

- [ ] **Step 1: 写失败测试（先只写元数据部分，错别字测试在 Task 15 追加）**

`services/compare-algo/tests/test_metadata.py`：

```python
from app.angineer.adapter import adapt_document
from app.angineer.raw import validate_raw_document
from app.metadata.service import compare_meta_fields

from tests.conftest import make_raw_block, make_raw_doc


def _adapt_all(raw_docs):
    return [adapt_document(validate_raw_document(d)) for d in raw_docs]


def _by_metric_key(evidences, key):
    return {e.metrics.get(key) or e.metrics.get("pattern"): e for e in evidences}


def test_author_match_mid(ir_docs):
    evidences = compare_meta_fields("task-001", ir_docs)
    e = _by_metric_key(evidences, "field")["author"]
    assert e.type == "metadata"
    assert e.severity == "mid"
    assert e.docIds == ["doc-a", "doc-b"]
    assert e.metrics["value"] == "张三"
    assert e.aiGenerated is False


def test_created_at_match_mid(ir_docs):
    evidences = compare_meta_fields("task-001", ir_docs)
    e = _by_metric_key(evidences, "field")["createdAt"]
    assert e.severity == "mid"
    assert e.docIds == ["doc-a", "doc-b"]
    assert e.metrics["value"] == "2026-07-01T10:00:00"


def test_created_at_pdf_dates_normalized_and_matched():
    # 真实产物 createdAt 为 PDF 原始日期：适配层归一 ISO 后参与相等性比对
    raw_x = make_raw_doc("doc-x", file_name="x.pdf", author=None,
                         created_at="D:20260701100000+08'00'",
                         blocks=[make_raw_block("p1", "甲方正文内容")])
    raw_y = make_raw_doc("doc-y", file_name="y.pdf", author=None,
                         created_at="D:20260701100000+08'00'",
                         blocks=[make_raw_block("p1", "乙方正文内容")])
    evidences = compare_meta_fields("task-001", _adapt_all([raw_x, raw_y]))
    e = _by_metric_key(evidences, "field")["createdAt"]
    assert e.severity == "mid"
    assert e.metrics["value"] == "2026-07-01T10:00:00+08:00"


def test_creator_tool_all_same_low(ir_docs):
    evidences = compare_meta_fields("task-001", ir_docs)
    e = _by_metric_key(evidences, "field")["creatorTool"]
    assert e.severity == "low"
    assert e.docIds == ["doc-a", "doc-b", "doc-c"]
    assert e.metrics["value"] == "Microsoft Word"


def test_meta_evidence_locations_have_empty_block_ids(ir_docs):
    evidences = compare_meta_fields("task-001", ir_docs)
    for e in evidences:
        assert all(l.blockIds == [] for l in e.locations)


def test_null_meta_fields_ignored(ir_doc_a):
    # author 为 null 的文档不参与比对（v2 §5-7：提取不到给 null）
    doc_x = ir_doc_a.model_copy(update={"docId": "doc-x"})
    doc_x.meta.author = None
    doc_y = ir_doc_a.model_copy(update={"docId": "doc-y"})
    doc_y.meta.author = None
    evidences = compare_meta_fields("task-001", [doc_x, doc_y])
    assert all(e.metrics.get("field") != "author" for e in evidences)


def test_fixture_metadata_evidence_count(ir_docs):
    # author + createdAt + creatorTool = 3 条（错别字证据在 Task 15 加入）
    assert len(compare_meta_fields("task-001", ir_docs)) == 3


def test_real_pingshen_pair_metadata(raw_pingshen_pair):
    """评审办法副本对（实测）：author 相同 + creatorTool 均 Writer + createdAt 不同。

    → author mid + creatorTool low，两条证据，无 createdAt 证据。
    """
    evidences = compare_meta_fields("task-real", _adapt_all(raw_pingshen_pair))
    by_field = _by_metric_key(evidences, "field")
    assert set(by_field) == {"author", "creatorTool"}
    assert by_field["author"].severity == "mid"
    assert by_field["creatorTool"].severity == "low"
    assert by_field["author"].docIds == ["doc-020a5d97", "doc-1d0c4891"]
```

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_metadata.py -q
```

- [ ] **Step 2: 实现 `app/metadata/service.py` 的 `compare_meta_fields`**

`services/compare-algo/app/metadata/service.py`：

```python
"""元数据比对：author / createdAt 一致性 + creatorTool 弱线索。metadata 类证据。"""
from app.schemas.evidence import Evidence, EvidenceLocation, Severity, build_evidence
from app.schemas.ir import IrDocument


def _group_by_meta(documents: list[IrDocument], attr: str) -> dict[str, list[IrDocument]]:
    """按 meta 字段值分组；None/空串不参与（v2 §5-7：提取不到给 null）。"""
    groups: dict[str, list[IrDocument]] = {}
    for d in documents:
        v = getattr(d.meta, attr)
        if v:
            groups.setdefault(v, []).append(d)
    return groups


def _meta_evidence(
    task_id: str,
    field: str,
    value: str,
    docs: list[IrDocument],
    severity: Severity,
    title: str,
    description: str,
) -> Evidence:
    ordered = sorted(docs, key=lambda d: d.docId)
    return build_evidence(
        task_id=task_id,
        type="metadata",
        severity=severity,
        doc_ids=[d.docId for d in ordered],
        locations=[EvidenceLocation(docId=d.docId) for d in ordered],
        metrics={"field": field, "value": value},
        title=title,
        description=description,
    )


def compare_meta_fields(task_id: str, documents: list[IrDocument]) -> list[Evidence]:
    evidences: list[Evidence] = []
    for author, docs in sorted(_group_by_meta(documents, "author").items()):
        if len(docs) >= 2:
            evidences.append(_meta_evidence(
                task_id, "author", author, docs, "mid",
                f"{len(docs)} 份标书文件作者相同（{author}）",
                "文件元数据作者一致，疑似同一台设备/同一人编制。",
            ))
    for created, docs in sorted(_group_by_meta(documents, "createdAt").items()):
        if len(docs) >= 2:
            evidences.append(_meta_evidence(
                task_id, "createdAt", created, docs, "mid",
                f"{len(docs)} 份标书创建时间完全相同（{created}）",
                "文件创建时间完全一致，疑似同一批次生成。",
            ))
    for tool, docs in sorted(_group_by_meta(documents, "creatorTool").items()):
        if len(docs) == len(documents) and len(docs) >= 2:
            evidences.append(_meta_evidence(
                task_id, "creatorTool", tool, docs, "low",
                f"全部标书使用同一编制工具（{tool}）",
                "编制工具一致仅为弱线索，需结合其他证据判断。",
            ))
    return evidences
```

- [ ] **Step 3: 运行测试确认通过（Task 15 的错别字断言此时尚未加入）**

```bash
cd services/compare-algo && uv run pytest tests/test_metadata.py -q
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): 元数据 author/createdAt/creatorTool 一致性比对（PDF 日期归一集成）"
```

---

## Task 15: 相同错别字检测（低频错字 n-gram 碰撞）

**Files:**
- Modify: `services/compare-algo/app/metadata/service.py`（追加 `detect_shared_typos` 与 `analyze_metadata`）
- Modify: `services/compare-algo/tests/test_metadata.py`（追加错别字测试）

原理（tech 决策：低频错字 n-gram 碰撞）：单文档内「全文仅出现一次的字符」多为生僻字或错别字；包含该字符且在全文仅出现一次的 6-gram 是「可疑异常串」。可疑串在 ≥2 份文档中逐字相同 = 「错得一样」，原生文本下是围标强证据（spec §4.5 的设计意图）。

- [ ] **Step 1: 追加失败测试**

在 `services/compare-algo/tests/test_metadata.py` 末尾追加：

```python
# ---------- Task 15：相同错别字检测 ----------

from app.metadata.service import analyze_metadata, detect_shared_typos


def _typo_doc(doc_id: str, text: str):
    raw = make_raw_doc(
        doc_id,
        file_name=f"{doc_id}.pdf",
        author=None,
        created_at=None,
        blocks=[make_raw_block("b001", text)],
    )
    return adapt_document(validate_raw_document(raw))


def test_shared_typo_detected():
    # 「保证今」为故意错别字（金→今），两份文档各出现一次
    doc_x = _typo_doc("doc-x", "我方缴纳履约保证今拾万元整。")
    doc_y = _typo_doc("doc-y", "贵方缴纳履约保证今拾万元整。")
    evidences = detect_shared_typos("task-001", [doc_x, doc_y])
    assert len(evidences) == 1
    e = evidences[0]
    assert e.type == "metadata"
    assert e.severity == "high"
    assert e.docIds == ["doc-x", "doc-y"]
    assert e.metrics["pattern"] == "shared-typo"
    assert e.metrics["sharedNgramCount"] >= 1
    assert any("今" in s for s in e.metrics["samples"])
    assert e.aiGenerated is False


def test_shared_typo_locations_point_to_blocks():
    doc_x = _typo_doc("doc-x", "我方缴纳履约保证今拾万元整。")
    doc_y = _typo_doc("doc-y", "贵方缴纳履约保证今拾万元整。")
    e = detect_shared_typos("task-001", [doc_x, doc_y])[0]
    assert {l.docId: l.blockIds for l in e.locations} == {"doc-x": ["b001"], "doc-y": ["b001"]}


def test_no_shared_typo_for_distinct_docs():
    doc_x = _typo_doc("doc-x", "我方缴纳履约保证金拾万元整。")
    doc_y = _typo_doc("doc-y", "贵司提交质量保修书原件备查。")
    assert detect_shared_typos("task-001", [doc_x, doc_y]) == []


def test_repeated_common_ngram_not_flagged():
    # 同一 n-gram 在单文档内出现多次 → 非低频，不算错字碰撞
    doc_x = _typo_doc("doc-x", "投标保证投标保证投标保证。")
    doc_y = _typo_doc("doc-y", "投标保证投标保证投标保证。")
    assert detect_shared_typos("task-001", [doc_x, doc_y]) == []


def test_fixture_shared_typo(ir_docs):
    # doc-a/doc-b 共享段内含「保证今」；doc-c 独立
    evidences = detect_shared_typos("task-001", ir_docs)
    assert len(evidences) == 1
    assert evidences[0].docIds == ["doc-a", "doc-b"]
    assert evidences[0].metrics["sharedNgramCount"] >= 6  # 6 个窗口覆盖「今」


def test_analyze_metadata_combines_both(ir_docs):
    evidences = analyze_metadata("task-001", ir_docs)
    # author + createdAt + creatorTool + shared-typo = 4 条
    assert len(evidences) == 4
    kinds = {e.metrics.get("field") or e.metrics.get("pattern") for e in evidences}
    assert kinds == {"author", "createdAt", "creatorTool", "shared-typo"}
```

注意：Task 14 顶部已 import `compare_meta_fields`、`adapt_document`、`validate_raw_document`、`make_raw_block`、`make_raw_doc`，此处只需追加 `analyze_metadata` 与 `detect_shared_typos` 的 import。

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_metadata.py -q
```

- [ ] **Step 2: 在 `app/metadata/service.py` 末尾追加实现**

在 `services/compare-algo/app/metadata/service.py` 末尾追加：

```python
# ---------- 相同错别字检测：低频错字 n-gram 碰撞 ----------

from app.ocr import is_low_confidence_ocr
from app.similarity.shingle import SHINGLABLE_TYPES, normalize_text

TYPO_NGRAM = 6
_TYPO_SAMPLES_MAX = 10


def _block_typo_ngrams(doc: IrDocument, n: int = TYPO_NGRAM) -> dict[str, set[str]]:
    """blockId -> 可疑异常 n-gram 集合。

    可疑 = 该 gram 在全文仅出现一次，且包含「全文仅出现一次的字符」
    （生僻字/错别字特征；常规用字会多次出现，被自然过滤）。
    低置信 OCR 块不参与（spec §4.5：OCR「错得一样」可能只是识别器犯了同样的错）。
    """
    eligible = [
        b for b in doc.blocks
        if b.type in SHINGLABLE_TYPES and not is_low_confidence_ocr(b)
    ]
    full_text = "".join(normalize_text(b.text) for b in eligible)
    if len(full_text) < n:
        return {}
    gram_freq: dict[str, int] = {}
    for i in range(len(full_text) - n + 1):
        g = full_text[i : i + n]
        gram_freq[g] = gram_freq.get(g, 0) + 1
    char_freq: dict[str, int] = {}
    for ch in full_text:
        char_freq[ch] = char_freq.get(ch, 0) + 1
    result: dict[str, set[str]] = {}
    for b in eligible:
        text = normalize_text(b.text)
        grams = {
            text[i : i + n]
            for i in range(max(0, len(text) - n + 1))
            if gram_freq.get(text[i : i + n], 0) == 1
            and any(char_freq[c] == 1 for c in text[i : i + n])
        }
        if grams:
            result[b.blockId] = grams
    return result


def detect_shared_typos(
    task_id: str, documents: list[IrDocument], n: int = TYPO_NGRAM
) -> list[Evidence]:
    """相同错别字：可疑 n-gram 在 ≥2 份文档中逐字碰撞 → high 证据。

    同一文档组合的多条碰撞归并为一条证据，locations 定位到含碰撞串的块。
    """
    gram_index: dict[str, dict[str, str]] = {}  # gram -> {docId: blockId}
    for d in documents:
        for block_id, grams in _block_typo_ngrams(d, n).items():
            for g in grams:
                gram_index.setdefault(g, {})[d.docId] = block_id

    by_docs: dict[tuple[str, ...], list[tuple[str, dict[str, str]]]] = {}
    for g, m in gram_index.items():
        if len(m) >= 2:
            by_docs.setdefault(tuple(sorted(m)), []).append((g, m))

    evidences: list[Evidence] = []
    for doc_ids, hits in sorted(by_docs.items()):
        locations = [
            EvidenceLocation(
                docId=doc_id,
                blockIds=sorted({m[doc_id] for _, m in hits}),
            )
            for doc_id in doc_ids
        ]
        samples = sorted({g for g, _ in hits})[:_TYPO_SAMPLES_MAX]
        evidences.append(build_evidence(
            task_id=task_id,
            type="metadata",
            severity="high",
            doc_ids=list(doc_ids),
            locations=locations,
            metrics={
                "pattern": "shared-typo",
                "sharedNgramCount": len(hits),
                "samples": samples,
            },
            title=f"{len(doc_ids)} 份标书出现相同错别字/低频异常串（{len(hits)} 处）",
            description="多份标书出现逐字相同的低频异常字串；原生文本中「错得一样」是围标强证据。",
        ))
    return evidences


def analyze_metadata(task_id: str, documents: list[IrDocument]) -> list[Evidence]:
    return compare_meta_fields(task_id, documents) + detect_shared_typos(task_id, documents)
```

- [ ] **Step 3: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_metadata.py -q
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): 相同错别字低频 n-gram 碰撞检测与 metadata 证据组装"
```

## Task 16: FastAPI 三个接口与统一错误处理（产物契约）

**Files:**
- Create: `services/compare-algo/app/schemas/api.py`
- Create: `services/compare-algo/app/main.py`
- Create: `services/compare-algo/tests/test_api.py`

接口（tech 决策已拍板）：`POST /analyze/similarity`、`POST /analyze/pricing`、`POST /analyze/metadata`，请求体 `{taskId, documents: [{docId, blocks, meta}, ...]}`（**AnGIneer 产物形态**，2~5 份），响应 `{evidences: [...]}`。处理流水线：产物校验（`RawDocumentEnvelope`）→ 适配（`adapt_document`）→ 分析 → 证据。产物校验或适配输出校验不合格返回 422 + 具体字段路径（`code=IR_VALIDATION_FAILED`，`details[].path/message`）；未知异常返回 500 + `code=INTERNAL_ERROR`，不泄露堆栈给调用方（记日志）。

- [ ] **Step 1: 写失败测试**

`services/compare-algo/tests/test_api.py`：

```python
from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def test_healthz():
    r = client.get("/healthz")
    assert r.status_code == 200
    assert r.json() == {"status": "ok"}


def test_similarity_endpoint(ir_payload):
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 200
    evidences = r.json()["evidences"]
    assert len(evidences) == 1
    e = evidences[0]
    # Evidence 字段逐字遵守 spec §6.1
    assert set(e) == {
        "id", "taskId", "type", "severity", "docIds",
        "locations", "metrics", "title", "description", "aiGenerated",
    }
    assert e["type"] == "similarity"
    assert e["taskId"] == "task-001"
    assert e["aiGenerated"] is False


def test_pricing_endpoint(ir_payload):
    r = client.post("/analyze/pricing", json=ir_payload)
    assert r.status_code == 200
    evidences = r.json()["evidences"]
    assert len(evidences) == 1
    assert evidences[0]["type"] == "pricing"
    assert evidences[0]["metrics"]["pattern"] == "arithmetic"


def test_metadata_endpoint(ir_payload):
    r = client.post("/analyze/metadata", json=ir_payload)
    assert r.status_code == 200
    evidences = r.json()["evidences"]
    assert len(evidences) == 4
    kinds = {e["metrics"].get("field") or e["metrics"].get("pattern") for e in evidences}
    assert kinds == {"author", "createdAt", "creatorTool", "shared-typo"}
    assert all(e["type"] == "metadata" for e in evidences)


def test_invalid_bbox_returns_422_with_field_path(ir_payload):
    # bbox 超出 0~1 归一化区间（疑似像素坐标）→ 422；块级校验在字段级 validator 中报出
    ir_payload["documents"][0]["blocks"][0]["bbox"] = [0, 0, 99999, 10]
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 422
    body = r.json()
    assert body["code"] == "IR_VALIDATION_FAILED"
    assert any("documents" in d["path"] for d in body["details"])
    assert any("bbox" in d["message"] for d in body["details"])


def test_missing_docmeta_field_returns_422(ir_payload):
    del ir_payload["documents"][0]["meta"]["docMeta"]["author"]  # 可 null 不可省略
    r = client.post("/analyze/metadata", json=ir_payload)
    assert r.status_code == 422
    assert r.json()["code"] == "IR_VALIDATION_FAILED"


def test_unknown_source_value_returns_422(ir_payload):
    # 实测词表 text/ocr/table/formula/null；其他值（如 v2 文档措辞 "native"）拒收
    ir_payload["documents"][0]["blocks"][0]["source"] = "native"
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 422


def test_single_document_rejected(ir_payload):
    ir_payload["documents"] = ir_payload["documents"][:1]
    r = client.post("/analyze/pricing", json=ir_payload)
    assert r.status_code == 422


def test_too_many_documents_rejected(ir_payload):
    ir_payload["documents"] = ir_payload["documents"] + ir_payload["documents"]  # 6 份
    r = client.post("/analyze/pricing", json=ir_payload)
    assert r.status_code == 422


def test_duplicate_doc_ids_rejected(ir_payload):
    ir_payload["documents"] = ir_payload["documents"][:2]
    ir_payload["documents"][1] = dict(ir_payload["documents"][0])
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 422


def test_empty_body_rejected():
    r = client.post("/analyze/similarity", json={})
    assert r.status_code == 422


def test_real_fixtures_end_to_end(raw_haigang_pair, raw_pingshen_pair):
    """真实产物端到端：海港对出 low 雷同证据；评审办法对出 2 条元数据证据。"""
    r = client.post("/analyze/similarity", json={
        "taskId": "task-real", "documents": raw_haigang_pair,
    })
    assert r.status_code == 200
    evidences = r.json()["evidences"]
    assert len(evidences) == 1
    assert evidences[0]["severity"] == "low"
    assert evidences[0]["docIds"] == ["doc-12f45ca9", "doc-c8be9f8b"]

    r = client.post("/analyze/metadata", json={
        "taskId": "task-real", "documents": raw_pingshen_pair,
    })
    assert r.status_code == 200
    kinds = {e["metrics"].get("field") for e in r.json()["evidences"]}
    assert kinds == {"author", "creatorTool"}
```

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_api.py -q
```

- [ ] **Step 2: 实现 `app/schemas/api.py`**

`services/compare-algo/app/schemas/api.py`：

```python
"""请求/响应与错误模型。请求为 AnGIneer 产物形态（v2 修订）；文档份数约束 2~5（spec §1）。"""
from pydantic import BaseModel, Field, model_validator

from app.angineer.raw import RawDocumentEnvelope
from app.schemas.evidence import Evidence


class AnalyzeRequest(BaseModel):
    taskId: str = Field(min_length=1)
    documents: list[RawDocumentEnvelope] = Field(min_length=2, max_length=5)

    @model_validator(mode="after")
    def _check_unique_doc_ids(self) -> "AnalyzeRequest":
        ids = [d.docId for d in self.documents]
        dups = sorted({i for i in ids if ids.count(i) > 1})
        if dups:
            raise ValueError(f"documents 中 docId 重复：{dups}")
        return self


class AnalyzeResponse(BaseModel):
    evidences: list[Evidence]


class ErrorDetail(BaseModel):
    path: str
    message: str


class ErrorResponse(BaseModel):
    code: str
    message: str
    details: list[ErrorDetail] = Field(default_factory=list)
```

- [ ] **Step 3: 实现 `app/main.py`**

`services/compare-algo/app/main.py`：

```python
"""compare-algo FastAPI 入口：三个分析接口 + 统一错误处理。

无状态计算服务，由 ABP 主服务调用；请求体为 AnGIneer 解析产物原文
（doc_blocks_graph.jsonl 节点 + meta 的 {docMeta, outlines, pages}），
本服务不直接对接 AnGIneer/MinerU。产物经 app/angineer/ 适配层转为内部模型后分析。
"""
import logging

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse
from pydantic import ValidationError

from app.angineer.adapter import adapt_document
from app.metadata.service import analyze_metadata
from app.pricing.service import analyze_pricing
from app.schemas.api import (
    AnalyzeRequest,
    AnalyzeResponse,
    ErrorDetail,
    ErrorResponse,
)
from app.similarity.service import analyze_similarity

logger = logging.getLogger("compare-algo")

app = FastAPI(title="compare-algo", version="0.1.0")


def _validation_error_body(errors) -> ErrorResponse:
    details = [
        ErrorDetail(
            path=".".join(str(p) for p in e.get("loc", ())),
            message=e.get("msg", ""),
        )
        for e in errors
    ]
    return ErrorResponse(
        code="IR_VALIDATION_FAILED",
        message="产物校验失败，详见 details",
        details=details,
    )


@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request: Request, exc: RequestValidationError) -> JSONResponse:
    """请求体（产物）校验不合格 → 422 + 具体字段错误（tech 决策）。"""
    body = _validation_error_body(exc.errors())
    return JSONResponse(status_code=422, content=body.model_dump())


@app.exception_handler(ValidationError)
async def adapt_exception_handler(request: Request, exc: ValidationError) -> JSONResponse:
    """适配输出违反内部模型底线（如 outline 引用缺失）→ 422。"""
    body = _validation_error_body(exc.errors())
    return JSONResponse(status_code=422, content=body.model_dump())


@app.exception_handler(Exception)
async def unhandled_exception_handler(request: Request, exc: Exception) -> JSONResponse:
    """未知异常 → 500，堆栈只进日志不进响应体。"""
    logger.exception("unhandled error on %s: %s", request.url.path, exc)
    body = ErrorResponse(code="INTERNAL_ERROR", message="内部分析失败，请联系算法服务负责人")
    return JSONResponse(status_code=500, content=body.model_dump())


@app.get("/healthz")
def healthz() -> dict[str, str]:
    return {"status": "ok"}


def _adapt(req: AnalyzeRequest):
    return [adapt_document(d) for d in req.documents]


@app.post("/analyze/similarity", response_model=AnalyzeResponse)
def post_analyze_similarity(req: AnalyzeRequest) -> AnalyzeResponse:
    return AnalyzeResponse(evidences=analyze_similarity(req.taskId, _adapt(req)))


@app.post("/analyze/pricing", response_model=AnalyzeResponse)
def post_analyze_pricing(req: AnalyzeRequest) -> AnalyzeResponse:
    return AnalyzeResponse(evidences=analyze_pricing(req.taskId, _adapt(req)))


@app.post("/analyze/metadata", response_model=AnalyzeResponse)
def post_analyze_metadata(req: AnalyzeRequest) -> AnalyzeResponse:
    return AnalyzeResponse(evidences=analyze_metadata(req.taskId, _adapt(req)))
```

- [ ] **Step 4: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_api.py -q
```

- [ ] **Step 5: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): FastAPI 三个分析接口（产物契约）与统一错误处理（422 字段级错误）"
```

---

## Task 17: 全量回归、README 与端到端冒烟

**Files:**
- Create: `services/compare-algo/README.md`

- [ ] **Step 1: 全量回归**

```bash
cd services/compare-algo && uv run pytest -q
```

预期：全部 passed（含合成 fixture 三域集成测试 + 4 份真实产物 fixture 的适配/分析/接口断言）。

- [ ] **Step 2: 写 README**

`services/compare-algo/README.md`：

````markdown
# compare-algo 比标算法服务

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
校验不合格：422 `{"code": "IR_VALIDATION_FAILED", "message": "...", "details": [{"path", "message"}]}`。

注意：请求体大小与文档体量正相关（实测 1965 块文档约 2~3MB），5 份上限约 15MB，内部 HTTP 调用可接受。

## 测试 fixture

- `tests/conftest.py`：3 份合成标书（raw 产物形态），覆盖低置信 OCR 降权、等差报价、相同错别字场景。
- `tests/fixtures/*.json`：4 份真实 AnGIneer 产物裁剪样本（海港1/海港2 部分雷同对、评审办法副本对）。
  重新生成方法见计划 `docs/superpowers/plans/2026-07-29-bid-compare-algo-service.md` Task 2
  （需要本机 AnGIneer 数据目录；字段裁剪清单以该 Task 为准）。
````

- [ ] **Step 3: 启动服务做端到端冒烟**

```bash
cd services/compare-algo && uv run uvicorn app.main:app --port 8100 &
sleep 3
curl -s http://127.0.0.1:8100/healthz
# 预期输出 {"status":"ok"}
curl -s -X POST http://127.0.0.1:8100/analyze/similarity -H "Content-Type: application/json" \
  -d @<(python -c "import json,pathlib;f=pathlib.Path('tests/fixtures');print(json.dumps({'taskId':'smoke','documents':[json.loads((f/'haigang1.json').read_text(encoding='utf-8')),json.loads((f/'haigang2.json').read_text(encoding='utf-8'))]}))")
# 预期输出含 1 条 similarity 证据（severity=low）
kill %1
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "docs(compare-algo): README（启动/测试/接口契约/fixture 再生）"
```

---

## 自查：硬性要求 → 任务覆盖映射

| spec/v2/实测 条款 | 覆盖位置 |
|---|---|
| v2 修订契约：直收 AnGIneer 产物（`{docId, blocks, meta}`），docId opaque | Task 3 `RawDocumentEnvelope`（`test_docid_is_opaque_no_prefix_check`）+ Task 16 `AnalyzeRequest` |
| 产物宽松校验：未知字段忽略、词表外 source 拒收、bbox 0~1 或 null、docMeta 可 null 不可省略、pages 非空正浮点 | Task 3 `raw.py` + `test_angineer_raw.py` 全量用例 |
| 实测 source 词表 `text/ocr/table/formula/null`（v2 "native" 系措辞） | Task 3 `RawSource` Literal + `test_source_vocabulary_enforced`；Task 4 `BlockSource` |
| 实测 chart→image、未知类型→para、page_number→footer | Task 4 `_TYPE_MAP` + `test_chart_mapped_to_image` / `test_unknown_type_falls_back_to_para` / `test_furniture_mapping` |
| 标题剥 HTML 标签（实测含 `<sub>`） | Task 4 `_strip_html` + `test_html_tags_stripped` |
| PDF 原始日期 → ISO（不可解析/ISO 原样） | Task 4 `pdf_date.py` + `TestPdfDate` 6 用例 + Task 14 `test_created_at_pdf_dates_normalized_and_matched` |
| outline 扁平→嵌套、锚点存在性 | Task 4 `_nest_outlines` + `TestOutlineNesting` + `IrDocument._check_document` |
| table 必须有 imgPath、html 可选（实测 2/132 无 html）；html 纯净 | Task 3 纯净度校验；Task 4 `IrBlock` 守卫 + `test_table_without_html_allowed`；pricing 跳过在 Task 13 `_best_price_block` + `test_table_without_html_skipped` |
| v2 §4 source/confidence 可 null（降权关闭）；source=text 时 confidence 必须 1.0；低置信照常交付 | Task 3/4 字段约束与正反测试；降权在 Task 6/11 |
| v2 §2 blockId=block_uid 文档内唯一；blocks 阅读顺序 | Task 3 唯一性校验；阅读顺序在 Task 9 `_shinglable_positions` 按 blocks 原序对齐 |
| 真实数据页眉跨文档相同 → furniture 不查重（防伪雷同） | Task 7 `SHINGLABLE_TYPES` 排除 header/footer + `test_block_shingles_skips_furniture` |
| §4.3.7 文本按段落级聚合，查重对齐以段落为单位 | Task 7 块级 shingle + Task 9 块级对齐（聚合由 doc_blocks_graph 天然按块给出） |
| §4.5 OCR 低置信降权/单独标注（null 时降权关闭） | Task 6 判定与降级（null 安全） + Task 11 `ocrSuspect` 接入（severity 降级 + 文案标注） |
| §6.1 Evidence 字段逐字 + aiGenerated 标记 | Task 5 模型与 `build_evidence`（恒 False）+ Task 16 接口字段集合断言 |
| §1 标书 2~5 份 | Task 16 `AnalyzeRequest.documents` min/max + 两个边界测试 |
| 真实产物端到端（海港对 low 雷同 / 评审办法对元数据 / 海港对 pricing 负路径） | Task 11 `test_real_haigang_pair_low_evidence` 等 + Task 14 `test_real_pingshen_pair_metadata` + Task 16 `test_real_fixtures_end_to_end` |
| tech 决策：查重流水线/报价规律/元数据+错别字/三接口/422 字段错误 | Task 7~11 / Task 12~13 / Task 14~15 / Task 16 |

## 明确不做（与 spec §2 及任务范围对齐）

- 不产出 `clause` / `indicator` 证据（compare-ai 语义服务职责）；Evidence 模型保留完整 Literal 仅为契约兼容。
- 不做公式语义等价判断（spec §2 非目标）。
- 不做任务状态机、产物下载/存储、报告生成/导出（compare-task / report 服务职责）。
- 不消费 `content.md` / `images/` / `mineru_raw/*`（content.md 是面向 LLM 语义层的有损投影；images 由前端按 imgPath 自取）。
- 相似度矩阵（`/matrix`）由 ABP 主服务基于本服务返回的 pair evidence.metrics.similarity 组装，本服务不单独出矩阵接口。
