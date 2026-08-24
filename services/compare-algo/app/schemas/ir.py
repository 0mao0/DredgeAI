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


class IrTableCell(BaseModel):
    model_config = ConfigDict(extra="forbid")

    row: int = Field(ge=0)
    col: int = Field(ge=0)
    rowspan: int = Field(default=1, ge=1)
    colspan: int = Field(default=1, ge=1)
    # 跨页表格单元格按页归属：高亮时用 cell.pageIdx 而非表格块主页面
    pageIdx: int = Field(ge=0)
    bbox: tuple[float, float, float, float]  # 0~1 归一化
    text: str = ""

    @field_validator("bbox")
    @classmethod
    def _check_bbox(cls, v):
        x0, y0, x1, y1 = v
        if x0 < 0 or y0 < 0 or x1 < x0 or y1 < y0 or max(v) > 1.0:
            raise ValueError(f"单元格 bbox {list(v)} 非法（须 0~1 归一化且 x1>=x0, y1>=y0）")
        return v


class IrTable(BaseModel):
    model_config = ConfigDict(extra="forbid")

    # 实测 2/132 表格无 table_html（有整表截图）：html 可选，imgPath 必须
    html: Optional[str] = None
    imgPath: str
    cells: list[IrTableCell] = Field(default_factory=list)


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
    role: Literal["bid", "tender"] = "bid"
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
        # pages 保留页面真实尺寸（不参与分析）；AnGIneer 实测 pages 数组可能被截断
        # （如 200 页）而块 page_idx 可达 213，缺页块不拒绝，仅保留原始 pageIdx 供溯源。
        page_map = {p.pageIdx: p for p in self.pages}
        if len(page_map) != len(self.pages):
            raise ValueError("pages 中 pageIdx 重复")
        return self
