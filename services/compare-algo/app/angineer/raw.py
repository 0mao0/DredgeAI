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

# table html 纯净结构（实测表格内可嵌 <img>，如检验检测报告样品图）
_ALLOWED_TABLE_TAGS = {"table", "tr", "td", "th", "img"}
_ALLOWED_TABLE_ATTRS = {"rowspan", "colspan", "src", "alt", "width", "height"}

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

    @field_validator("bbox", mode="before")
    @classmethod
    def _check_bbox_length(cls, v):
        # 非 4 元素序列在 tuple 强转时会塌陷为 "Field required"，须在此之前给出明确文案
        if v is None:
            return v
        if not isinstance(v, (list, tuple)) or len(v) != 4:
            raise ValueError(f"bbox 必须为 4 元素数组，收到 {v!r}")
        return v

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
        v = v.strip()
        if not v:
            return None  # AnGIneer 对缺失 html 的表格输出空串，等价于缺省
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
            raise ValueError(f"文档 {self.docId} 的 block_uid 重复：{dups}")
        return self


def validate_raw_document(data: dict) -> RawDocumentEnvelope:
    """产物校验入口：不合格抛 pydantic.ValidationError（含具体字段路径）。"""
    return RawDocumentEnvelope.model_validate(data)
