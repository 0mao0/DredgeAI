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
