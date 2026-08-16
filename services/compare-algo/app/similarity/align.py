"""块级精确对齐与相似度计算。

候选块对按 A 侧阅读顺序排列后，用 difflib.SequenceMatcher 在 B 侧位置序列上
求不交叉的单调递增匹配链，剔除交叉冲突。SequenceMatcher 是启发式：产出合法的
不交叉链，但不保证全局最长——本场景只需稳定剔除交叉冲突，不要求最优。
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
    # 只对可查重块计算规范化长度（候选对命中的块必在其中）；非查重块跳过归一化开销
    norm_len_a = {
        b.blockId: len(normalize_text(b.text)) for b in doc_a.blocks if b.blockId in pos_a
    }
    norm_len_b = {
        b.blockId: len(normalize_text(b.text)) for b in doc_b.blocks if b.blockId in pos_b
    }

    # 按 A 侧阅读顺序排列候选对，在 B 侧位置序列上取不交叉单调链（启发式）
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
    # 候选对命中块必为可查重块（∈ pos_*），get 兜底防御直接构造的越界对
    chars_a = sum(norm_len_a.get(bid, 0) for bid in matched_a)
    chars_b = sum(norm_len_b.get(bid, 0) for bid in matched_b)
    total_a = sum(norm_len_a[bid] for bid in pos_a)
    total_b = sum(norm_len_b[bid] for bid in pos_b)
    similarity = 0.0
    if total_a + total_b > 0:
        similarity = round((chars_a + chars_b) / (total_a + total_b), 4)
    return PairSimilarityResult(doc_a.docId, doc_b.docId, similarity, matches)
