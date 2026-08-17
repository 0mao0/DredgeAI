"""局部雷同检测：跨块全文长公共子串（LCS）。

块级查重（minhash + block Jaccard≥0.5 召回）对「语义段相同但块切分不同、
相同片段占块比例低」的局部雷同（如服务承诺）会漏检：核心句被拆进不同块后
单块 Jaccard 常常不足 0.5。本模块在规范化全文上直接寻找 ≥min_len 的
连续公共片段，不依赖块边界，输出片段文本、字数与双方块级定位；
可选传入招标文件全文，为每个片段标记「是否招标文件响应」。
"""
from __future__ import annotations

from typing import Optional

from app.display import display_names
from app.schemas.evidence import Evidence, EvidenceLocation, Severity, build_evidence
from app.schemas.ir import IrDocument
from app.settings import get_settings
from app.similarity.shingle import SHINGLABLE_TYPES, normalize_text

# 默认值文档；运行期以 settings.passage_min_len 为准
MIN_PASSAGE_LEN = 20
INDEX_NGRAM = 20
SEVERITY_HIGH_LEN = 80
SEVERITY_MID_LEN = 40

# 参与局部雷同的块类型与块级查重一致；仅原生文本块（source == "text"）：
# OCR 对同一模板犯同一错不是逐字复制证据，混入会产生伪雷同（与 typo 检测同口径）
_TEXT_BLOCK_TYPES = SHINGLABLE_TYPES


def _passage_blocks(doc: IrDocument):
    return [
        b for b in doc.blocks
        if b.type in _TEXT_BLOCK_TYPES and b.source == "text" and normalize_text(b.text)
    ]


def full_text(doc: IrDocument) -> str:
    """文档规范化全文：仅参与查重的原生文本块按阅读顺序拼接（与 typo 检测同口径）。"""
    return "".join(normalize_text(b.text) for b in _passage_blocks(doc))


def block_offsets(doc: IrDocument) -> list[tuple[str, int, int, int]]:
    """(blockId, pageIdx, start, end)：规范化全文偏移，供片段定位到块/页。"""
    out: list[tuple[str, int, int, int]] = []
    pos = 0
    for b in _passage_blocks(doc):
        t = normalize_text(b.text)
        if not t:
            continue
        out.append((b.blockId, b.pageIdx, pos, pos + len(t)))
        pos += len(t)
    return out


def _covering_blocks(
    offsets: list[tuple[str, int, int, int]],
    start: int,
    end: int,
) -> tuple[list[str], list[int]]:
    """片段 [start, end) 覆盖的 blockId 与页码（按阅读顺序去重）。"""
    blocks: list[str] = []
    pages: list[int] = []
    for block_id, page_idx, bs, be in offsets:
        if be > start and bs < end:
            if block_id not in blocks:
                blocks.append(block_id)
            if page_idx not in pages:
                pages.append(page_idx)
    return blocks, pages


def find_common_passages(
    text_a: str,
    text_b: str,
    offsets_a,
    offsets_b,
    min_len: int = MIN_PASSAGE_LEN,
) -> list[tuple[int, int, int]]:
    """规范化全文 A/B 上找 ≥min_len 的连续公共片段。

    返回 [(a_start, b_start, length), ...]（按 A 侧阅读顺序；同一区域已扩展为最长并跳过，
    不会对同一片段重复产出）。a_start/b_start 分别是片段在 A/B 全文中的起始偏移，
    调用方必须用各自侧偏移定位——混用会导致高亮错位（如 B 侧误落到投标人名称块）。
    """
    n = INDEX_NGRAM
    if len(text_a) < min_len or len(text_b) < min_len:
        return []

    # B 侧 20-gram 位置索引（gram → 起始偏移列表）
    index: dict[str, list[int]] = {}
    for i in range(len(text_b) - n + 1):
        index.setdefault(text_b[i : i + n], []).append(i)

    passages: list[tuple[int, int, int]] = []
    i = 0
    while i <= len(text_a) - min_len:
        hits = index.get(text_a[i : i + n])
        if not hits:
            i += 1
            continue

        best_len = 0
        best_start = i
        best_b_start = -1
        for pos in hits:
            # 双向扩展：先向左、再向右，取该命中点能覆盖的最长连续公共段
            left = 0
            while (
                i - left - 1 >= 0
                and pos - left - 1 >= 0
                and text_a[i - left - 1] == text_b[pos - left - 1]
            ):
                left += 1
            total = left
            si, sj = i - left, pos - left
            while (
                si + total < len(text_a)
                and sj + total < len(text_b)
                and text_a[si + total] == text_b[sj + total]
            ):
                total += 1
            if total > best_len:
                best_len, best_start = total, si
                best_b_start = sj

        if best_len >= min_len:
            passages.append((best_start, best_b_start, best_len))
            i = best_start + best_len  # 跳过已覆盖区域，避免重复产出
        else:
            i += 1
    return passages


def _severity_of_longest(length: int) -> Severity:
    if length >= SEVERITY_HIGH_LEN:
        return "high"
    if length >= SEVERITY_MID_LEN:
        return "mid"
    return "low"


def _tender_gram_index(tender_text: str) -> set[str]:
    """招标文件规范化全文的 20-gram 集合（供片段主体匹配率快速判断）。"""
    n = INDEX_NGRAM
    if len(tender_text) < n:
        return set()
    return {tender_text[i : i + n] for i in range(len(tender_text) - n + 1)}


def tender_ratio(text: str, tender_text: str, tender_grams: set[str]) -> float:
    """片段与招标文件的主体重合率（0~1）。

    片段整体在招标文件中 → 1.0；否则按片段 20-gram 在招标文件中的命中比例近似。
    片段可能夹带相邻公共尾巴（如「本项目完成后」），只要主体响应招标即应判为招标响应。
    """
    if text in tender_text:
        return 1.0
    n = INDEX_NGRAM
    if len(text) < n or not tender_grams:
        return 0.0
    hits = sum(1 for i in range(len(text) - n + 1) if text[i : i + n] in tender_grams)
    return hits / (len(text) - n + 1)


def local_similarity_evidences(task_id: str, documents: list[IrDocument]) -> list[Evidence]:
    """投标文件两两之间的局部雷同证据（type=similarity, metrics.kind=passage）。

    招标文件（role="tender"，可选）存在时，逐片段标记 tenderResponse：
    该片段同时出现在招标文件全文 → True（响应招标，通常不构成雷同）；
    False → 企业自写内容雷同（真正需要人工复核的候选）。
    """
    min_len = get_settings().passage_min_len
    tender = next((d for d in documents if d.role == "tender"), None)
    tender_text = full_text(tender) if tender is not None else None
    tender_grams = _tender_gram_index(tender_text) if tender_text is not None else None
    bids = [d for d in documents if d.role != "tender"]
    names = display_names(bids)

    evidences: list[Evidence] = []
    for i in range(len(bids)):
        for j in range(i + 1, len(bids)):
            a, b = bids[i], bids[j]
            ta, tb = full_text(a), full_text(b)
            offsets_a, offsets_b = block_offsets(a), block_offsets(b)
            passages = find_common_passages(ta, tb, offsets_a, offsets_b, min_len)
            if not passages:
                continue

            items: list[dict] = []
            all_blocks_a: list[str] = []
            all_blocks_b: list[str] = []
            for a_start, b_start, length in passages:
                text = ta[a_start : a_start + length]
                blocks_a, pages_a = _covering_blocks(offsets_a, a_start, a_start + length)
                blocks_b, pages_b = _covering_blocks(offsets_b, b_start, b_start + length)
                tender_response: Optional[bool] = None
                ratio: Optional[float] = None
                if tender_text is not None:
                    ratio = tender_ratio(text, tender_text, tender_grams)
                    tender_response = ratio >= 0.8
                items.append({
                    "text": text,
                    "length": length,
                    "docA": {"blockIds": blocks_a, "pages": pages_a},
                    "docB": {"blockIds": blocks_b, "pages": pages_b},
                    "tenderResponse": tender_response,
                    **({"tenderRatio": round(ratio, 3)} if ratio is not None else {}),
                })
                for bid in blocks_a:
                    if bid not in all_blocks_a:
                        all_blocks_a.append(bid)
                for bid in blocks_b:
                    if bid not in all_blocks_b:
                        all_blocks_b.append(bid)

            longest = max(item["length"] for item in items)
            tender_hits = sum(1 for item in items if item["tenderResponse"] is True)
            desc = "两份文档存在跨块连续文本相同，疑似模板套用或复制粘贴。"
            if tender_text is not None:
                desc += (
                    f"其中 {tender_hits} 处为招标文件响应，"
                    f"{len(items) - tender_hits} 处为企业自写内容雷同。"
                )
            evidences.append(build_evidence(
                task_id=task_id,
                type="similarity",
                severity=_severity_of_longest(longest),
                doc_ids=[a.docId, b.docId],
                locations=[
                    EvidenceLocation(docId=a.docId, blockIds=all_blocks_a),
                    EvidenceLocation(docId=b.docId, blockIds=all_blocks_b),
                ],
                metrics={
                    "kind": "passage",
                    "passages": items,
                    "longestLength": longest,
                    "passageCount": len(items),
                },
                title=f"{names[a.docId]} 与 {names[b.docId]} 存在 {len(items)} 处局部雷同（最长 {longest} 字）",
                description=desc,
            ))
    return evidences
