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
