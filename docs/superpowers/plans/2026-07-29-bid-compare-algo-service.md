# 比标算法服务 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 新建独立 Python 微服务 `compare-algo`，消费 MinerU 产出的 IR（ir.json），产出结构化确定性证据项（Evidence），覆盖 P1 全部能力：IR schema 校验、两两查重（shingling + MinHash/LSH 粗筛 + 块级对齐）、雷同簇聚类、OCR 降权、报价规律分析（等差/尾数/贴近度）、元数据与相同错别字比对、FastAPI 三个分析接口与统一错误处理。

**Architecture:** 无状态计算服务，由 ABP 主服务（compare-task）通过 HTTP 调用。请求体为任务内 2~5 份 IR 的 JSON，响应为 Evidence 列表。服务内部分层：`schemas/`（pydantic IR/Evidence/请求响应模型与校验）→ `similarity/`、`pricing/`、`metadata/`（三个确定性分析域，各自纯函数 + service 组装证据）→ `main.py`（FastAPI 接口与异常处理器）。本服务只产出 `aiGenerated=false` 的 `similarity|pricing|metadata` 三类证据；`clause|indicator` 属 compare-ai 语义服务，不在此实现。

**Tech Stack:** Python 3.11 + FastAPI + pydantic v2 + datasketch（MinHash/LSH）+ beautifulsoup4/lxml（表格 html）+ pytest + httpx（TestClient）+ uv（包管理，pyproject.toml 单一事实源）

**假设（已在设计中拍板）:**
- 查重算法：段落级 n-gram shingling（中文按字 trigram 为主）→ datasketch MinHash/LSH 粗筛候选对 → difflib 块级精确对齐 → 输出相似度与对齐块列表；雷同簇聚类（≥3 份共同雷同单独标记）。
- 报价分析：从表格 html 中解析数值（正则 + 千分位/万元单位归一），检测等差、尾数规律、贴近度。
- 元数据比对：author/creatorTool/createdAt 一致性 + 相同错别字检测（低频错字 n-gram 碰撞）。
- OCR 降权：`source=ocr` 且 `confidence<0.5` 的块单独标注，不作为强证据（spec §4.5）。
- 表格 html 解析用 beautifulsoup4 / lxml。
- 测试：pytest，TDD（每个任务先写失败测试再实现）。
- 包管理用 **uv**（`uv sync` / `uv run pytest`），全计划统一，不混用 pip。
- MinerU 产物由 ABP 主服务传递，本服务不直接对接 MinerU。
- 服务落在本 monorepo 的 `services/compare-algo/`（与 ABP 主服务解耦部署，仅 HTTP 契约耦合）。
- 唯一事实源：`docs/superpowers/specs/2026-07-29-ai-bid-compare-design.md`（下称 spec），Evidence 字段名逐字遵守 spec §6.1，IR 硬性要求逐字遵守 spec §4。

---

## 决策摘要

| 决策项 | 选择 |
|---|---|
| 包管理 | uv（pyproject.toml + `[dependency-groups]` dev） |
| 服务位置 | `services/compare-algo/`（新目录，不动现有 user-web/admin-web） |
| 相似度阈值 | LSH 粗筛 0.5；精确 Jaccard 复核 ≥0.5；出证据 Dice ≥0.3；severity high ≥0.8 / mid ≥0.5 / low 其余 |
| 雷同簇 | Union-Find 归并两两相似度 ≥0.5 的文档，簇 ≥3 份单独出 high 证据 |
| OCR 降权 | 命中低置信 OCR 块的证据 severity 降一级 + `metrics.ocrSuspect=true` + 文案标注 |
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
│   ├── schemas/
│   │   ├── __init__.py
│   │   ├── ir.py                # IR pydantic 模型 + spec §4.3 硬性校验
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
    ├── conftest.py              # 3 份虚拟标书 IR fixture（A/B 雷同+同作者+同错别字+等差报价，C 独立）
    ├── test_smoke.py
    ├── test_ir_schema.py
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

---

## Task 1: 项目脚手架（uv + pyproject + 目录骨架 + pytest 冒烟）

**Files:**
- Create: `services/compare-algo/pyproject.toml`
- Create: `services/compare-algo/app/__init__.py`（空文件）
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
description = "比标算法服务：消费 MinerU IR，产出确定性 Evidence"
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
mkdir -p services/compare-algo/app/schemas services/compare-algo/app/similarity services/compare-algo/app/pricing services/compare-algo/app/metadata services/compare-algo/tests
touch services/compare-algo/app/__init__.py services/compare-algo/app/schemas/__init__.py services/compare-algo/app/similarity/__init__.py services/compare-algo/app/pricing/__init__.py services/compare-algo/app/metadata/__init__.py services/compare-algo/tests/__init__.py
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

## Task 2: IR pydantic schema 校验器（spec §4.2/§4.3 硬性要求）

**Files:**
- Create: `services/compare-algo/tests/conftest.py`（3 份虚拟标书 fixture，后续所有 Task 复用）
- Create: `services/compare-algo/tests/test_ir_schema.py`
- Create: `services/compare-algo/app/schemas/ir.py`

覆盖的硬性要求（spec §4.3）：bbox 页面实际像素坐标（非负、x1≥x0、不超出 `pages[].width/height`）；每块必带 `source`/`confidence`（native 必须 1.0；低置信照常接收）；`blockId` 文档内唯一；outline 引用必须存在；table 必须同时给 `html`+`imgPath` 且 html 纯净（仅 table/tr/td/th、仅 rowspan/colspan 属性）；equation 必须给 LaTeX 文本；image/seal/equation 必须给 `imgPath`；meta 四字段可 null 不可省略。

- [ ] **Step 1: 写测试 fixture（conftest.py）与失败测试，运行确认失败**

`services/compare-algo/tests/conftest.py`：

```python
"""测试 fixture：3 份虚拟标书 IR。

- doc-a / doc-b：共享一段雷同承诺文本（内含故意错别字「保证今」，各出现一次）、
  同一作者「张三」、同一 createdAt、报价 1,000,000 / 1,010,000（与 doc-c 构成等差）。
- doc-b 另有一个 source=ocr 且 confidence=0.3 的低置信块，文本与 doc-a 的 b004 完全相同，
  用于 OCR 降权测试（spec §4.5）。
- doc-c：内容、作者、时间均独立，报价 1,020,000（三份构成公差 10,000 的等差数列）。
- 三份 creatorTool 均为 "Microsoft Word"（全相同 → 低危线索）。
"""
import pytest

from app.schemas.ir import IrDocument, validate_ir_document

PAGE = {"pageIdx": 0, "width": 1190, "height": 1684}

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


def make_block(
    block_id: str,
    text: str,
    *,
    type: str = "para",
    source: str = "native",
    confidence: float = 1.0,
    page_idx: int = 0,
    y: int = 0,
    table: dict | None = None,
) -> dict:
    block: dict = {
        "blockId": block_id,
        "pageIdx": page_idx,
        "bbox": [50, 100 + y, 1140, 140 + y],
        "type": type,
        "text": text,
        "textLevel": 1 if type == "title" else 0,
        "source": source,
        "confidence": confidence,
    }
    if table is not None:
        block["table"] = table
    return block


def price_table(total: str) -> dict:
    return {
        "html": (
            "<table>"
            "<tr><th>项目</th><th>金额</th></tr>"
            "<tr><td>分部分项工程费</td><td>800,000.00</td></tr>"
            "<tr><td>措施费</td><td>100,000.00</td></tr>"
            f"<tr><td>投标总价（元）</td><td>{total}</td></tr>"
            "</table>"
        ),
        "imgPath": "images/price.jpg",
    }


def make_ir(
    doc_id: str,
    *,
    file_name: str,
    author: str | None,
    created_at: str | None,
    blocks: list[dict],
) -> dict:
    return {
        "schemaVersion": "1.0",
        "docId": doc_id,
        "meta": {
            "fileName": file_name,
            "pageCount": 1,
            "author": author,
            "creatorTool": "Microsoft Word",
            "createdAt": created_at,
            "modifiedAt": None,
        },
        "pages": [dict(PAGE)],
        "outline": [],
        "blocks": blocks,
    }


@pytest.fixture()
def ir_doc_a() -> IrDocument:
    return validate_ir_document(make_ir(
        "doc-a",
        file_name="A公司投标文件.pdf",
        author="张三",
        created_at="2026-07-01T10:00:00",
        blocks=[
            make_block("b001", "投标函", type="title", y=0),
            make_block("b002", A_INTRO, y=60),
            make_block("b003", SHARED_PARAGRAPH, y=120),
            make_block("b004", A_SEAL_LINE, y=180),
            make_block("b005", "", type="table", y=240, table=price_table("1,000,000.00")),
        ],
    ))


@pytest.fixture()
def ir_doc_b() -> IrDocument:
    return validate_ir_document(make_ir(
        "doc-b",
        file_name="B公司投标文件.pdf",
        author="张三",
        created_at="2026-07-01T10:00:00",
        blocks=[
            make_block("b001", "投标函", type="title", y=0),
            make_block("b002", B_INTRO, y=60),
            make_block("b003", SHARED_PARAGRAPH, y=120),
            make_block("b004", "", type="table", y=180, table=price_table("1,010,000.00")),
            # 低置信 OCR 块，文本与 doc-a 的 b004 完全相同 → 雷同但须降权（spec §4.5）
            make_block("b005", A_SEAL_LINE, source="ocr", confidence=0.3, y=240),
        ],
    ))


@pytest.fixture()
def ir_doc_c() -> IrDocument:
    return validate_ir_document(make_ir(
        "doc-c",
        file_name="C公司投标文件.pdf",
        author="李四",
        created_at="2026-07-02T09:30:00",
        blocks=[
            make_block("b001", C_TITLE, type="title", y=0),
            make_block("b002", C_PARA_1, y=60),
            make_block("b003", C_PARA_2, y=120),
            make_block("b004", "", type="table", y=180, table=price_table("1,020,000.00")),
        ],
    ))


@pytest.fixture()
def ir_docs(ir_doc_a: IrDocument, ir_doc_b: IrDocument, ir_doc_c: IrDocument) -> list[IrDocument]:
    return [ir_doc_a, ir_doc_b, ir_doc_c]


@pytest.fixture()
def ir_payload(ir_docs: list[IrDocument]) -> dict:
    """可直接 POST 的请求体（Task 14 接口测试用）。"""
    return {"taskId": "task-001", "documents": [d.model_dump(mode="json") for d in ir_docs]}
```

`services/compare-algo/tests/test_ir_schema.py`：

```python
import pytest
from pydantic import ValidationError

from app.schemas.ir import validate_ir_document


def _minimal_ir() -> dict:
    return {
        "schemaVersion": "1.0",
        "docId": "d1",
        "meta": {
            "fileName": "a.pdf",
            "pageCount": 1,
            "author": None,
            "creatorTool": None,
            "createdAt": None,
            "modifiedAt": None,
        },
        "pages": [{"pageIdx": 0, "width": 100, "height": 100}],
        "outline": [],
        "blocks": [
            {
                "blockId": "b1",
                "pageIdx": 0,
                "bbox": [0, 0, 50, 10],
                "type": "para",
                "text": "正文内容",
                "textLevel": 0,
                "source": "native",
                "confidence": 1.0,
            }
        ],
    }


def test_fixture_docs_are_valid(ir_docs):
    assert [d.docId for d in ir_docs] == ["doc-a", "doc-b", "doc-c"]


def test_minimal_ir_passes():
    doc = validate_ir_document(_minimal_ir())
    assert doc.docId == "d1"


def test_meta_field_must_be_present_even_if_null():
    # spec §4.2：提取不到给 null，不省略字段
    data = _minimal_ir()
    del data["meta"]["author"]
    with pytest.raises(ValidationError):
        validate_ir_document(data)


def test_bbox_out_of_page_rejected():
    # spec §4.3.1：bbox 必须是页面实际像素坐标
    data = _minimal_ir()
    data["blocks"][0]["bbox"] = [0, 0, 1000, 1000]  # 页面仅 100x100
    with pytest.raises(ValidationError):
        validate_ir_document(data)


def test_bbox_negative_or_inverted_rejected():
    for bad in ([-1, 0, 50, 10], [50, 0, 10, 10], [0, 20, 50, 10]):
        data = _minimal_ir()
        data["blocks"][0]["bbox"] = bad
        with pytest.raises(ValidationError):
            validate_ir_document(data)


def test_confidence_out_of_range_rejected():
    data = _minimal_ir()
    data["blocks"][0]["confidence"] = 1.5
    with pytest.raises(ValidationError):
        validate_ir_document(data)


def test_native_confidence_must_be_1():
    # spec §4.2：native 文本给 1.0
    data = _minimal_ir()
    data["blocks"][0]["confidence"] = 0.8
    with pytest.raises(ValidationError):
        validate_ir_document(data)


def test_low_confidence_ocr_block_accepted():
    # spec §4.3.2：低置信块照常交付，不得静默丢弃
    data = _minimal_ir()
    data["blocks"][0]["source"] = "ocr"
    data["blocks"][0]["confidence"] = 0.3
    doc = validate_ir_document(data)
    assert doc.blocks[0].confidence == 0.3


def test_table_requires_html_and_imgpath():
    # spec §4.3.4：表格必须同时给 html 与整表截图
    data = _minimal_ir()
    data["blocks"][0]["type"] = "table"
    data["blocks"][0]["text"] = ""
    with pytest.raises(ValidationError):
        validate_ir_document(data)


def test_table_html_purity_rejects_class_style_and_foreign_tags():
    data = _minimal_ir()
    data["blocks"][0]["type"] = "table"
    data["blocks"][0]["text"] = ""
    for bad_html in (
        '<table class="t"><tr><td>1</td></tr></table>',
        '<table><tr><td style="color:red">1</td></tr></table>',
        '<table><thead><tr><td>1</td></tr></thead></table>',
    ):
        data["blocks"][0]["table"] = {"html": bad_html, "imgPath": "images/t.jpg"}
        with pytest.raises(ValidationError):
            validate_ir_document(data)


def test_table_html_with_rowspan_colspan_passes():
    data = _minimal_ir()
    data["blocks"][0]["type"] = "table"
    data["blocks"][0]["text"] = ""
    data["blocks"][0]["table"] = {
        "html": '<table><tr><td rowspan="2">a</td><td colspan="2">b</td></tr><tr><td>c</td><td>d</td></tr></table>',
        "imgPath": "images/t.jpg",
    }
    doc = validate_ir_document(data)
    assert doc.blocks[0].table is not None


def test_equation_requires_latex_text():
    # spec §4.3.6：行间公式 text 必须给 LaTeX 源码，不允许只给截图
    data = _minimal_ir()
    data["blocks"][0]["type"] = "equation"
    data["blocks"][0]["text"] = ""
    data["blocks"][0]["imgPath"] = "images/e.png"
    with pytest.raises(ValidationError):
        validate_ir_document(data)


def test_equation_with_latex_passes():
    data = _minimal_ir()
    data["blocks"][0]["type"] = "equation"
    data["blocks"][0]["text"] = "E=mc^2"
    data["blocks"][0]["imgPath"] = "images/e.png"
    doc = validate_ir_document(data)
    assert doc.blocks[0].text == "E=mc^2"


def test_image_and_seal_require_imgpath():
    for t in ("image", "seal"):
        data = _minimal_ir()
        data["blocks"][0]["type"] = t
        data["blocks"][0]["text"] = ""
        with pytest.raises(ValidationError):
            validate_ir_document(data)


def test_duplicate_block_id_rejected():
    data = _minimal_ir()
    data["blocks"].append(dict(data["blocks"][0]))
    with pytest.raises(ValidationError):
        validate_ir_document(data)


def test_outline_unknown_block_id_rejected():
    data = _minimal_ir()
    data["outline"] = [{"title": "第一章", "level": 1, "blockId": "nope", "children": []}]
    with pytest.raises(ValidationError):
        validate_ir_document(data)


def test_block_page_idx_must_exist_in_pages():
    data = _minimal_ir()
    data["blocks"][0]["pageIdx"] = 5
    with pytest.raises(ValidationError):
        validate_ir_document(data)
```

运行确认失败（此时 `app/schemas/ir.py` 尚不存在，collection 报 ImportError 即视为失败）：

```bash
cd services/compare-algo && uv run pytest tests/test_ir_schema.py -q
```

- [ ] **Step 2: 实现 `app/schemas/ir.py`**

`services/compare-algo/app/schemas/ir.py`：

```python
"""IR（ir.json）pydantic 校验模型。

契约见 spec §4.2（字段名逐字遵守），硬性要求见 spec §4.3。
校验不合格抛 pydantic.ValidationError，错误中带具体字段路径。
"""
from __future__ import annotations

from typing import Literal, Optional

from lxml import html as lxml_html
from pydantic import BaseModel, ConfigDict, Field, field_validator, model_validator

# spec §4.3.4：纯净结构（仅 table/tr/td/th，无样式无 class），仅允许合并单元格属性
_ALLOWED_TABLE_TAGS = {"table", "tr", "td", "th"}
_ALLOWED_TABLE_ATTRS = {"rowspan", "colspan"}

BlockType = Literal["title", "para", "table", "list", "image", "equation", "seal", "header", "footer"]
BlockSource = Literal["native", "ocr"]


class IrMeta(BaseModel):
    model_config = ConfigDict(extra="forbid")

    fileName: str
    pageCount: int = Field(ge=1)
    # spec §4.2：提取不到给 null，不省略字段（Optional 无默认值 = 必填可空）
    author: Optional[str]
    creatorTool: Optional[str]
    createdAt: Optional[str]
    modifiedAt: Optional[str]


class IrPage(BaseModel):
    model_config = ConfigDict(extra="forbid")

    pageIdx: int = Field(ge=0)
    width: int = Field(gt=0)
    height: int = Field(gt=0)


class IrOutlineNode(BaseModel):
    model_config = ConfigDict(extra="forbid")

    title: str
    level: int = Field(ge=1)
    blockId: str
    children: list["IrOutlineNode"] = Field(default_factory=list)


class IrTable(BaseModel):
    model_config = ConfigDict(extra="forbid")

    html: str
    imgPath: str

    @field_validator("html")
    @classmethod
    def _check_html_purity(cls, v: str) -> str:
        try:
            root = lxml_html.fromstring(v)
        except Exception as exc:
            raise ValueError(f"table.html 不是合法 HTML：{exc}") from exc
        for el in root.iter():
            tag = el.tag if isinstance(el.tag, str) else ""
            if tag not in _ALLOWED_TABLE_TAGS:
                raise ValueError(f"table.html 含非法标签 <{tag}>，仅允许 table/tr/td/th")
            for attr in el.attrib:
                if attr not in _ALLOWED_TABLE_ATTRS:
                    raise ValueError(f"table.html 含非法属性 {attr!r}，仅允许 rowspan/colspan")
        return v


class IrBlock(BaseModel):
    model_config = ConfigDict(extra="forbid")

    blockId: str = Field(min_length=1)
    pageIdx: int = Field(ge=0)
    bbox: tuple[float, float, float, float]
    type: BlockType
    text: str = ""
    textLevel: int = Field(default=0, ge=0)
    source: BlockSource
    confidence: float = Field(ge=0.0, le=1.0)
    table: Optional[IrTable] = None
    imgPath: Optional[str] = None

    @field_validator("bbox")
    @classmethod
    def _check_bbox_shape(cls, v: tuple[float, float, float, float]) -> tuple[float, float, float, float]:
        x0, y0, x1, y1 = v
        if x0 < 0 or y0 < 0:
            raise ValueError(f"bbox {list(v)} 坐标不得为负")
        if x1 < x0 or y1 < y0:
            raise ValueError(f"bbox {list(v)} 必须满足 x1>=x0 且 y1>=y0")
        return v

    @model_validator(mode="after")
    def _check_type_requirements(self) -> "IrBlock":
        # spec §4.2：native 文本 confidence 给 1.0
        if self.source == "native" and self.confidence != 1.0:
            raise ValueError(f"block {self.blockId}：source=native 时 confidence 必须为 1.0")
        # spec §4.3.4：table 必须同时给 html 与整表截图
        if self.type == "table" and self.table is None:
            raise ValueError(f"block {self.blockId}：type=table 必须提供 table(html+imgPath)")
        if self.type != "table" and self.table is not None:
            raise ValueError(f"block {self.blockId}：非 table 类型不得携带 table 字段")
        # spec §4.2：image / seal / equation 必须给 imgPath
        if self.type in ("image", "seal", "equation") and not self.imgPath:
            raise ValueError(f"block {self.blockId}：type={self.type} 必须提供 imgPath")
        # spec §4.3.6：行间公式 text 必须给 LaTeX 源码
        if self.type == "equation" and not self.text.strip():
            raise ValueError(f"block {self.blockId}：equation 块的 text 必须给 LaTeX 源码")
        return self


class IrDocument(BaseModel):
    model_config = ConfigDict(extra="forbid")

    schemaVersion: str
    docId: str = Field(min_length=1)
    meta: IrMeta
    pages: list[IrPage] = Field(min_length=1)
    outline: list[IrOutlineNode] = Field(default_factory=list)
    blocks: list[IrBlock] = Field(min_length=1)

    @model_validator(mode="after")
    def _check_document(self) -> "IrDocument":
        # spec §4.2：blockId 文档内唯一
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
        # pageIdx 必须存在；bbox 不得超出页面像素尺寸（spec §4.3.1：不接受归一化坐标）
        page_map = {p.pageIdx: p for p in self.pages}
        if len(page_map) != len(self.pages):
            raise ValueError("pages 中 pageIdx 重复")
        for b in self.blocks:
            page = page_map.get(b.pageIdx)
            if page is None:
                raise ValueError(f"block {b.blockId} 的 pageIdx={b.pageIdx} 在 pages 中不存在")
            x0, y0, x1, y1 = b.bbox
            if x1 > page.width or y1 > page.height:
                raise ValueError(
                    f"block {b.blockId} 的 bbox {list(b.bbox)} 超出页面 {b.pageIdx} 尺寸 "
                    f"{page.width}x{page.height}（bbox 必须是页面实际像素坐标）"
                )
        return self


def validate_ir_document(data: dict) -> IrDocument:
    """IR schema 校验入口：不合格抛 pydantic.ValidationError（含具体字段路径）。"""
    return IrDocument.model_validate(data)
```

- [ ] **Step 3: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_ir_schema.py -q
```

预期：全部 passed。

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): IR pydantic schema 校验器（bbox/source/confidence/表格纯净度/公式 LaTeX）"
```

---

## Task 3: Evidence 模型与组装器（spec §6.1 字段逐字）

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

## Task 4: OCR 低置信降权工具（spec §4.5）

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
    assert is_low_confidence_ocr(a_b4) is False      # native
    assert is_low_confidence_ocr(b_b5) is True       # ocr + 0.3
    assert is_low_confidence_ocr(b_b3) is False      # native


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
    return block.source == "ocr" and block.confidence < OCR_LOW_CONFIDENCE_THRESHOLD


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

## Task 5: 文本规范化与字级 n-gram shingling

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

# 参与文本查重的块类型；table 由 pricing 域单独处理，header/footer 不查重
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

## Task 6: MinHash/LSH 粗筛候选块对

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
    # doc-b b005（OCR 低置信）与 doc-a b004 文本相同 → 仍是候选（降权在 Task 9 处理）
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

---

## Task 7: 块级精确对齐与相似度计算（difflib 单调对齐）

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

## Task 8: 雷同簇聚类（Union-Find，≥3 份共同雷同）

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

## Task 9: similarity 证据组装 service（含 OCR 降权接入）

**Files:**
- Create: `services/compare-algo/app/similarity/service.py`
- Create: `services/compare-algo/tests/test_similarity_service.py`

证据规则：文档对 Dice ≥0.3 出证据；severity：≥0.8 high / ≥0.5 mid / 其余 low；命中低置信 OCR 块 → severity 降一级 + `metrics.ocrSuspect=true` + 文案标注（spec §4.5）；雷同簇（≥3 份）单独出 high 证据，`metrics.cluster=true`。

- [ ] **Step 1: 写失败测试**

`services/compare-algo/tests/test_similarity_service.py`：

```python
from app.similarity.service import analyze_similarity


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
    assert len(ce.locations) == 3


def test_similarity_thresholds(ir_doc_a):
    # 相似度 < 0.3 不出证据：只保留一个极小重合块无法构造（候选 Jaccard 0.5 已过滤），
    # 这里验证阈值常量存在且单调
    from app.similarity import service
    assert service.SEVERITY_HIGH > service.SEVERITY_MID > service.EVIDENCE_MIN_SIMILARITY > 0
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
from app.similarity.minhash import build_block_index, find_candidate_pairs

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


def _pair_evidence(task_id: str, r: PairSimilarityResult, suspect: bool) -> Evidence:
    severity = _severity_of(r.similarity)
    if suspect:
        severity = downgrade_severity(severity)
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
        title=f"{r.doc_id_a} 与 {r.doc_id_b} 存在文本雷同（相似度 {r.similarity:.0%}）",
        description="两份文档存在大段雷同。"
        + ("部分雷同块来自扫描件 OCR 低置信识别，准确率可能受影响，已降权处理。" if suspect else ""),
    )


def analyze_similarity(task_id: str, documents: list[IrDocument]) -> list[Evidence]:
    index = build_block_index(documents)
    pairs = find_candidate_pairs(index)
    by_doc_pair: dict[tuple[str, str], list] = {}
    for p in pairs:
        by_doc_pair.setdefault((p.doc_id_a, p.doc_id_b), []).append(p)

    doc_map = {d.docId: d for d in documents}
    ocr_ids = {d.docId: low_confidence_ocr_block_ids(d) for d in documents}

    results: list[PairSimilarityResult] = []
    for (a, b), group in sorted(by_doc_pair.items()):
        results.append(align_document_pair(doc_map[a], doc_map[b], group))

    evidences: list[Evidence] = []
    for r in results:
        if r.similarity < EVIDENCE_MIN_SIMILARITY or not r.matches:
            continue
        suspect = any(
            m.block_id_a in ocr_ids[r.doc_id_a] or m.block_id_b in ocr_ids[r.doc_id_b]
            for m in r.matches
        )
        evidences.append(_pair_evidence(task_id, r, suspect))

    for members in find_similarity_clusters(results, CLUSTER_MIN_SIMILARITY):
        sub = [
            r for r in results
            if r.doc_id_a in members and r.doc_id_b in members and r.matches
        ]
        avg_sim = round(sum(r.similarity for r in sub) / len(sub), 4)
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
            severity="high",
            doc_ids=members,
            locations=locations,
            metrics={"cluster": True, "memberCount": len(members), "avgSimilarity": avg_sim},
            title=f"{len(members)} 份标书存在共同雷同（{', '.join(members)}）",
            description="≥3 份标书经两两高相似传递归并构成雷同簇，是围串标强信号。",
        ))
    return evidences
```

- [ ] **Step 3: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_similarity_service.py -q
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): similarity 证据组装 service（OCR 降权 + 雷同簇）"
```

---

## Task 10: 报价表格解析与数值归一

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

    def test_extract_amounts_collects_all(self):
        grid = [["a", "100"], ["200", "c"]]
        assert extract_amounts(grid) == [100.0, 200.0]


def test_fixture_price_tables(ir_doc_a):
    table_block = next(b for b in ir_doc_a.blocks if b.type == "table")
    grid = parse_table_html(table_block.table.html)
    assert extract_total_amount(grid) == 1000000.0
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
    return value * multiplier
```

- [ ] **Step 3: 实现 `app/pricing/table_parse.py`**

`services/compare-algo/app/pricing/table_parse.py`：

```python
"""表格 html 解析：rowspan/colspan 展开为完整网格（占位格填同一值）。

输入 html 已通过 IR schema 纯净度校验（仅 table/tr/td/th），此处不做安全过滤。
"""
from bs4 import BeautifulSoup

from app.pricing.number_norm import parse_amount

_TOTAL_KEYWORDS = ("总价", "合计", "投标报价", "总报价", "总金额")


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
    """优先取含 总价/合计/报价 关键词行中的最大金额；否则取全表最大金额。"""
    keyword_amounts: list[float] = []
    for row in grid:
        if any(k in "".join(row) for k in _TOTAL_KEYWORDS):
            for cell in row:
                v = parse_amount(cell)
                if v is not None:
                    keyword_amounts.append(v)
    if keyword_amounts:
        return max(keyword_amounts)
    amounts = extract_amounts(grid)
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

## Task 11: 报价规律检测与 pricing 证据组装

**Files:**
- Create: `services/compare-algo/app/pricing/patterns.py`
- Create: `services/compare-algo/app/pricing/service.py`
- Create: `services/compare-algo/tests/test_pricing_patterns.py`
- Create: `services/compare-algo/tests/test_pricing_service.py`

检测规则（tech 决策：等差、尾数规律、贴近度）：
- 等差：≥3 份，升序相邻差值的相对偏差 ≤1% → high；
- 尾数：≥3 份，整数部分末两位完全相同 → mid；
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
        r = detect_tail_pattern([10000, 20000, 30000])
        assert r is not None
        assert r.tail == "00"

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
    # 贴近度 20000/1020000≈1.96% > 1% 不触发，尾数 00/10/20 不同不触发
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
    """≥3 份报价整数部分末 tail_len 位完全相同（尾数规律，疑似同源编制）。"""
    if len(amounts) < 3:
        return None
    tails = [str(int(a)).zfill(tail_len)[-tail_len:] for a in amounts]
    if len(set(tails)) == 1:
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
    """取文档中投标总价最大的表格块，返回 (blockId, total)；无表格或无金额返回 None。"""
    best: tuple[str, float] | None = None
    for block in doc.blocks:
        if block.type != "table" or block.table is None:
            continue
        total = extract_total_amount(parse_table_html(block.table.html))
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

---

## Task 12: 元数据比对（author / createdAt / creatorTool）

**Files:**
- Create: `services/compare-algo/app/metadata/service.py`（本 Task 先实现 `compare_meta_fields`，Task 13 追加 `detect_shared_typos` 与 `analyze_metadata`）
- Create: `services/compare-algo/tests/test_metadata.py`

规则：author 相同（非 null，≥2 份）→ mid；createdAt 完全相同（非 null，≥2 份）→ mid；creatorTool 全部文档相同 → low（常见工具相同仅作弱线索）。

- [ ] **Step 1: 写失败测试（先只写元数据部分，错别字测试在 Task 13 追加）**

`services/compare-algo/tests/test_metadata.py`：

```python
from app.metadata.service import compare_meta_fields


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
    # author 为 null 的文档不参与比对（spec §4.2：提取不到给 null）
    doc_x = ir_doc_a.model_copy(update={"docId": "doc-x"})
    doc_x.meta.author = None
    doc_y = ir_doc_a.model_copy(update={"docId": "doc-y"})
    doc_y.meta.author = None
    evidences = compare_meta_fields("task-001", [doc_x, doc_y])
    assert all(e.metrics.get("field") != "author" for e in evidences)


def test_fixture_metadata_evidence_count(ir_docs):
    # author + createdAt + creatorTool = 3 条（错别字证据在 Task 13 加入）
    assert len(compare_meta_fields("task-001", ir_docs)) == 3
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
    """按 meta 字段值分组；None/空串不参与（spec §4.2：提取不到给 null）。"""
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

- [ ] **Step 3: 运行测试确认通过（Task 13 的错别字断言此时尚未加入）**

```bash
cd services/compare-algo && uv run pytest tests/test_metadata.py -q
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): 元数据 author/createdAt/creatorTool 一致性比对"
```

---

## Task 13: 相同错别字检测（低频错字 n-gram 碰撞）

**Files:**
- Modify: `services/compare-algo/app/metadata/service.py`（追加 `detect_shared_typos` 与 `analyze_metadata`）
- Modify: `services/compare-algo/tests/test_metadata.py`（追加错别字测试）

原理（tech 决策：低频错字 n-gram 碰撞）：单文档内「全文仅出现一次的字符」多为生僻字或错别字；包含该字符且在全文仅出现一次的 6-gram 是「可疑异常串」。可疑串在 ≥2 份文档中逐字相同 = 「错得一样」，原生文本下是围标强证据（spec §4.5 的设计意图）。

- [ ] **Step 1: 追加失败测试**

在 `services/compare-algo/tests/test_metadata.py` 末尾追加：

```python
# ---------- Task 13：相同错别字检测 ----------

from app.metadata.service import analyze_metadata, detect_shared_typos
from app.schemas.ir import validate_ir_document

from tests.conftest import make_block, make_ir


def _typo_doc(doc_id: str, text: str):
    return validate_ir_document(make_ir(
        doc_id,
        file_name=f"{doc_id}.pdf",
        author=None,
        created_at=None,
        blocks=[make_block("b001", text)],
    ))


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

注意：Task 12 顶部已 import `compare_meta_fields`，此处追加 `analyze_metadata` 与 `detect_shared_typos` 的 import（`analyze_metadata` 在本 Task 才实现）。

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

---

## Task 14: FastAPI 三个接口与统一错误处理

**Files:**
- Create: `services/compare-algo/app/schemas/api.py`
- Create: `services/compare-algo/app/main.py`
- Create: `services/compare-algo/tests/test_api.py`

接口（tech 决策已拍板）：`POST /analyze/similarity`、`POST /analyze/pricing`、`POST /analyze/metadata`，请求体 `{taskId, documents: [IR, ...]}`（2~5 份），响应 `{evidences: [...]}`。IR schema 校验不合格返回 422 + 具体字段路径（`code=IR_VALIDATION_FAILED`，`details[].path/message`）；未知异常返回 500 + `code=INTERNAL_ERROR`，不泄露堆栈给调用方（记日志）。

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


def test_invalid_ir_returns_422_with_field_path(ir_payload):
    # bbox 超出页面尺寸 → 422；跨字段校验（bbox vs 页面尺寸）在文档级 model_validator 中报出，
    # 因此 path 定位到出问题的文档，具体字段信息在 message 中
    ir_payload["documents"][0]["blocks"][0]["bbox"] = [0, 0, 99999, 10]
    r = client.post("/analyze/similarity", json=ir_payload)
    assert r.status_code == 422
    body = r.json()
    assert body["code"] == "IR_VALIDATION_FAILED"
    assert any("documents" in d["path"] for d in body["details"])
    assert any("bbox" in d["message"] for d in body["details"])


def test_missing_meta_field_returns_422(ir_payload):
    del ir_payload["documents"][0]["meta"]["author"]  # 可 null 不可省略
    r = client.post("/analyze/metadata", json=ir_payload)
    assert r.status_code == 422
    assert r.json()["code"] == "IR_VALIDATION_FAILED"


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
```

运行确认失败：

```bash
cd services/compare-algo && uv run pytest tests/test_api.py -q
```

- [ ] **Step 2: 实现 `app/schemas/api.py`**

`services/compare-algo/app/schemas/api.py`：

```python
"""请求/响应与错误模型。文档份数约束 2~5（spec §1）。"""
from pydantic import BaseModel, Field, model_validator

from app.schemas.evidence import Evidence
from app.schemas.ir import IrDocument


class AnalyzeRequest(BaseModel):
    taskId: str = Field(min_length=1)
    documents: list[IrDocument] = Field(min_length=2, max_length=5)

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

无状态计算服务，由 ABP 主服务调用；MinerU 产物（IR）随请求体传入，
本服务不直接对接 MinerU。
"""
import logging

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

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


@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request: Request, exc: RequestValidationError) -> JSONResponse:
    """IR schema 校验不合格 → 422 + 具体字段错误（tech 决策）。"""
    details = [
        ErrorDetail(
            path=".".join(str(p) for p in e.get("loc", ())),
            message=e.get("msg", ""),
        )
        for e in exc.errors()
    ]
    body = ErrorResponse(
        code="IR_VALIDATION_FAILED",
        message="IR schema 校验失败，详见 details",
        details=details,
    )
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


@app.post("/analyze/similarity", response_model=AnalyzeResponse)
def post_analyze_similarity(req: AnalyzeRequest) -> AnalyzeResponse:
    return AnalyzeResponse(evidences=analyze_similarity(req.taskId, req.documents))


@app.post("/analyze/pricing", response_model=AnalyzeResponse)
def post_analyze_pricing(req: AnalyzeRequest) -> AnalyzeResponse:
    return AnalyzeResponse(evidences=analyze_pricing(req.taskId, req.documents))


@app.post("/analyze/metadata", response_model=AnalyzeResponse)
def post_analyze_metadata(req: AnalyzeRequest) -> AnalyzeResponse:
    return AnalyzeResponse(evidences=analyze_metadata(req.taskId, req.documents))
```

- [ ] **Step 4: 运行测试确认通过**

```bash
cd services/compare-algo && uv run pytest tests/test_api.py -q
```

- [ ] **Step 5: 提交**

```bash
git add services/compare-algo && git commit -m "feat(compare-algo): FastAPI 三个分析接口与统一错误处理（422 字段级错误）"
```

---

## Task 15: 全量回归、README 与端到端冒烟

**Files:**
- Create: `services/compare-algo/README.md`

- [ ] **Step 1: 全量回归**

```bash
cd services/compare-algo && uv run pytest -q
```

预期：全部 passed（含 conftest fixture 驱动的三域集成测试）。

- [ ] **Step 2: 写 README**

`services/compare-algo/README.md`：

````markdown
# compare-algo 比标算法服务

无状态确定性计算服务：消费 MinerU IR（由 ABP 主服务随请求体传入，本服务不直接对接 MinerU），
产出 `aiGenerated=false` 的 Evidence（similarity / pricing / metadata）。
契约见 `docs/superpowers/specs/2026-07-29-ai-bid-compare-design.md` §4（IR）、§6.1（Evidence）。

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

请求体：`{"taskId": "...", "documents": [<ir.json>, ...]}`（2~5 份 IR）。
响应：`{"evidences": [Evidence, ...]}`。
IR 校验不合格：422 `{"code": "IR_VALIDATION_FAILED", "message": "...", "details": [{"path", "message"}]}`。
````

- [ ] **Step 3: 启动服务做端到端冒烟**

```bash
cd services/compare-algo && uv run uvicorn app.main:app --port 8100 &
sleep 3
curl -s http://127.0.0.1:8100/healthz
# 预期输出 {"status":"ok"}
kill %1
```

- [ ] **Step 4: 提交**

```bash
git add services/compare-algo && git commit -m "docs(compare-algo): README（启动/测试/接口契约）"
```

---

## 自查：spec §4 硬性要求 → 任务覆盖映射

| spec 条款 | 覆盖位置 |
|---|---|
| §4.2 字段定义（meta 四字段可 null 不可省略、pages、outline、blocks） | Task 2 `IrMeta`/`IrPage`/`IrOutlineNode`/`IrBlock` + `test_meta_field_must_be_present_even_if_null` |
| §4.3.1 bbox 页面实际像素坐标（拒绝归一化/越界/负值/倒置） | Task 2 `_check_bbox_shape` + `IrDocument._check_document` + 3 个 bbox 测试 |
| §4.3.2 每块必带 source/confidence；低置信照常交付不丢弃 | Task 2 字段约束 + `test_low_confidence_ocr_block_accepted`；降权在 Task 4/9 |
| §4.3.3 blockId 文档内唯一、blocks 阅读顺序 | Task 2 唯一性校验；阅读顺序在 Task 7 `_shinglable_positions` 按 blocks 原序对齐（blockId 跨重跑稳定性无法机器校验，属提供方承诺，不落任务） |
| §4.3.4 表格必须 html+整表截图；html 纯净（仅 table/tr/td/th、仅 rowspan/colspan） | Task 2 `IrTable._check_html_purity` + 纯净度正/反测试；解析在 Task 10 |
| §4.3.5 印章单独 seal 类型 | Task 2 `BlockType` Literal 含 `seal` + `test_image_and_seal_require_imgpath` |
| §4.3.6 行间公式独立成块且 text 给 LaTeX（参与查重） | Task 2 equation 校验 + Task 5 `SHINGLABLE_TYPES` 含 equation（LaTeX 进 shingle） |
| §4.3.7 文本按段落级聚合，查重对齐以段落为单位 | Task 5 块级 shingle + Task 7 块级对齐（聚合本身属提供方侧，不可机器校验） |
| §4.5 OCR 低置信降权/单独标注 | Task 4 判定与降级 + Task 9 `ocrSuspect` 接入（severity 降级 + 文案标注） |
| §6.1 Evidence 字段逐字 + aiGenerated 标记 | Task 3 模型与 `build_evidence`（恒 False）+ Task 14 接口字段集合断言 |
| §1 标书 2~5 份 | Task 14 `AnalyzeRequest.documents` min/max + 两个边界测试 |
| tech 决策：查重流水线/报价规律/元数据+错别字/三接口/422 字段错误 | Task 5~9 / Task 10~11 / Task 12~13 / Task 14 |

## 明确不做（与 spec §2 及任务范围对齐）

- 不产出 `clause` / `indicator` 证据（compare-ai 语义服务职责）；Evidence 模型保留完整 Literal 仅为契约兼容。
- 不做公式语义等价判断（spec §2 非目标）。
- 不做任务状态机、IR 持久化、报告生成/导出（compare-task / report 服务职责）。
- 相似度矩阵（`/matrix`）由 ABP 主服务基于本服务返回的 pair evidence.metrics.similarity 组装，本服务不单独出矩阵接口。
