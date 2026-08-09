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


def test_author_multiple_groups_each_evidence():
    # 同一字段多个取值分组：甲/乙两组各自出 author 证据（pin 子组语义）
    docs = _adapt_all([
        make_raw_doc("doc-w", file_name="w.pdf", author="甲", created_at=None,
                     blocks=[make_raw_block("p1", "内容一")]),
        make_raw_doc("doc-x", file_name="x.pdf", author="甲", created_at=None,
                     blocks=[make_raw_block("p1", "内容二")]),
        make_raw_doc("doc-y", file_name="y.pdf", author="乙", created_at=None,
                     blocks=[make_raw_block("p1", "内容三")]),
        make_raw_doc("doc-z", file_name="z.pdf", author="乙", created_at=None,
                     blocks=[make_raw_block("p1", "内容四")]),
    ])
    evidences = compare_meta_fields("task-001", docs)
    author_ev = [e for e in evidences if e.metrics.get("field") == "author"]
    assert len(author_ev) == 2
    by_value = {e.metrics["value"]: e for e in author_ev}
    assert set(by_value) == {"甲", "乙"}
    assert by_value["甲"].docIds == ["doc-w", "doc-x"]
    assert by_value["乙"].docIds == ["doc-y", "doc-z"]


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


def test_single_doc_no_evidence():
    # 单文档输入：无任何分组可成对 → 零证据
    docs = _adapt_all([
        make_raw_doc("doc-x", file_name="x.pdf", author="张三",
                     created_at="2026-07-01T10:00:00",
                     blocks=[make_raw_block("p1", "单文档内容")]),
    ])
    assert compare_meta_fields("task-001", docs) == []


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
