from app.similarity.minhash import (
    NUM_PERM,
    build_block_index,
    build_minhash,
    find_candidate_pairs,
)
from tests.conftest import adapt, make_raw_block, make_raw_doc


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


def test_lsh_key_separator_no_collision_with_slash_in_ids():
    # doc_id/block_id 含 "/" 时，"/" 拼接会让 ("a/b","c") 与 ("a","b/c") 都映射到
    # "a/b/c" 而互相覆盖；不可打印分隔符 \x1f 不与 id 字符碰撞
    text = "本工程采用框架剪力墙结构体系抗震设防烈度为七度"
    doc1 = adapt(make_raw_doc("a/b", file_name="1.pdf", author=None, created_at=None,
                              blocks=[make_raw_block("c", text)]))
    doc2 = adapt(make_raw_doc("a", file_name="2.pdf", author=None, created_at=None,
                              blocks=[make_raw_block("b/c", text)]))
    pairs = find_candidate_pairs(build_block_index([doc1, doc2]))
    assert len(pairs) == 1
    assert {pairs[0].doc_id_a, pairs[0].doc_id_b} == {"a/b", "a"}
