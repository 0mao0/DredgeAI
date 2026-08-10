from app.angineer.adapter import adapt_document
from app.angineer.raw import validate_raw_document
from app.metadata.service import analyze_metadata, compare_meta_fields, detect_shared_typos

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


def _typo_doc(doc_id: str, text: str, *, source: str | None = "text", confidence: float | None = 1.0):
    raw = make_raw_doc(
        doc_id,
        file_name=f"{doc_id}.pdf",
        author=None,
        created_at=None,
        blocks=[make_raw_block("b001", text, source=source, confidence=confidence)],
    )
    return adapt_document(validate_raw_document(raw))


def _typo_doc_multi(doc_id: str, texts: list[str]):
    raw = make_raw_doc(
        doc_id,
        file_name=f"{doc_id}.pdf",
        author=None,
        created_at=None,
        blocks=[make_raw_block(f"b{i:03d}", t) for i, t in enumerate(texts, 1)],
    )
    return adapt_document(validate_raw_document(raw))


def test_shared_typo_detected():
    # 「保证今」为故意错别字（金→今），两份文档各出现一次 → 去重为 1 处站点，mid
    doc_x = _typo_doc("doc-x", "我方缴纳履约保证今拾万元整。")
    doc_y = _typo_doc("doc-y", "贵方缴纳履约保证今拾万元整。")
    evidences = detect_shared_typos("task-001", [doc_x, doc_y])
    assert len(evidences) == 1
    e = evidences[0]
    assert e.type == "metadata"
    assert e.severity == "mid"
    assert e.docIds == ["doc-x", "doc-y"]
    assert e.metrics["pattern"] == "shared-typo"
    assert e.metrics["sharedNgramCount"] == 1
    assert any("今" in s for s in e.metrics["samples"])
    assert "涉及文件：doc-x.pdf、doc-y.pdf。" in e.description
    assert e.aiGenerated is False


def test_shared_typo_two_sites_high():
    # 两处独立错字站点（不同块，窗口互不重叠）→ 2 处 → high
    doc_x = _typo_doc_multi("doc-x", ["我方缴纳履约保证今拾万元整。", "本工程骏工验收合格后退还保修金。"])
    doc_y = _typo_doc_multi("doc-y", ["贵方缴纳履约保证今拾万元整。", "本工程骏工验收合格后退还保修金。"])
    evidences = detect_shared_typos("task-001", [doc_x, doc_y])
    assert len(evidences) == 1
    e = evidences[0]
    assert e.severity == "high"
    assert e.metrics["sharedNgramCount"] == 2
    assert len(e.metrics["samples"]) == 2
    assert any("今" in s for s in e.metrics["samples"])
    assert {l.docId: l.blockIds for l in e.locations} == {
        "doc-x": ["b001", "b002"], "doc-y": ["b001", "b002"],
    }


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


def test_shared_typo_ocr_blocks_ignored():
    # OCR 块不参与（高/低置信度均）：同一识别器对同一模板可能犯同样的错
    for confidence in (1.0, 0.3):
        doc_x = _typo_doc("doc-x", "我方缴纳履约保证今拾万元整。", source="ocr", confidence=confidence)
        doc_y = _typo_doc("doc-y", "贵方缴纳履约保证今拾万元整。", source="ocr", confidence=confidence)
        assert detect_shared_typos("task-001", [doc_x, doc_y]) == []


def test_shared_typo_null_source_blocks_ignored():
    # source 缺失（图片/图表块等）同样不参与错别字碰撞
    doc_x = _typo_doc("doc-x", "我方缴纳履约保证今拾万元整。", source=None, confidence=None)
    doc_y = _typo_doc("doc-y", "贵方缴纳履约保证今拾万元整。", source=None, confidence=None)
    assert detect_shared_typos("task-001", [doc_x, doc_y]) == []


def test_shared_typo_three_way_single_evidence():
    # ≥3 份碰撞：归并为一条证据，docIds 含全部 3 份
    doc_x = _typo_doc("doc-x", "我方缴纳履约保证今拾万元整。")
    doc_y = _typo_doc("doc-y", "贵方缴纳履约保证今拾万元整。")
    doc_z = _typo_doc("doc-z", "他方缴纳履约保证今拾万元整。")
    evidences = detect_shared_typos("task-001", [doc_x, doc_y, doc_z])
    assert len(evidences) == 1
    assert evidences[0].docIds == ["doc-x", "doc-y", "doc-z"]
    assert evidences[0].metrics["sharedNgramCount"] == 1


def test_formal_numeral_boilerplate_at_most_mid():
    # 回归：大写金额套话「人民币壹佰万元整」多窗口碰撞 → 合并为 1 处，mid 封顶（不得 high）
    doc_x = _typo_doc("doc-x", "投标报价为人民币壹佰万元整，详见附表。")
    doc_y = _typo_doc("doc-y", "投标总价为人民币壹佰万元整，详见附表。")
    evidences = detect_shared_typos("task-001", [doc_x, doc_y])
    assert len(evidences) == 1
    assert evidences[0].severity == "mid"
    assert evidences[0].metrics["sharedNgramCount"] == 1


def test_fixture_shared_typo(ir_docs):
    # doc-a/doc-b 共享段内含「保证今」；doc-c 独立。71 个碰撞窗口去重为 1 处 → mid
    evidences = detect_shared_typos("task-001", ir_docs)
    assert len(evidences) == 1
    assert evidences[0].docIds == ["doc-a", "doc-b"]
    assert evidences[0].severity == "mid"
    assert evidences[0].metrics["sharedNgramCount"] == 1
    assert len(evidences[0].metrics["samples"]) == 1


def test_shared_typo_skipped_for_identical_docs(raw_pingshen_pair):
    # 全文完全一致的副本对：雷同已由 similarity 证据覆盖，错字碰撞不再单独出证据
    # （实测事实 #7：评审办法副本对仅 author + creatorTool 两条元数据证据）
    assert detect_shared_typos("task-real", _adapt_all(raw_pingshen_pair)) == []


def test_analyze_metadata_combines_both(ir_docs):
    evidences = analyze_metadata("task-001", ir_docs)
    # author + createdAt + creatorTool + shared-typo = 4 条
    assert len(evidences) == 4
    kinds = {e.metrics.get("field") or e.metrics.get("pattern") for e in evidences}
    assert kinds == {"author", "createdAt", "creatorTool", "shared-typo"}
