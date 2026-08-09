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
