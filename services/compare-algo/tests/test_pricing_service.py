from app.pricing.service import analyze_pricing


def test_fixture_arithmetic_evidence(ir_docs):
    evidences = analyze_pricing("task-001", ir_docs)
    # 1,000,000 / 1,010,000 / 1,020,000：等差（公差 10,000），
    # 贴近度 20000/1020000≈1.96% > 1% 不触发，尾数全为 "00"（百元取整常态）排除不触发
    assert len(evidences) == 1
    e = evidences[0]
    assert e.type == "pricing"
    assert e.severity == "high"
    assert e.aiGenerated is False
    assert e.metrics["pattern"] == "arithmetic"
    assert e.metrics["commonDiff"] == 10000
    assert e.metrics["amounts"] == {"doc-a": 1000000.0, "doc-b": 1010000.0, "doc-c": 1020000.0}
    assert e.docIds == ["doc-a", "doc-b", "doc-c"]


def test_pricing_locations_point_to_table_blocks(ir_docs):
    e = analyze_pricing("task-001", ir_docs)[0]
    loc = {l.docId: l.blockIds for l in e.locations}
    assert loc == {"doc-a": ["b005"], "doc-b": ["b004"], "doc-c": ["b004"]}


def test_less_than_two_priced_docs_no_evidence(ir_doc_a):
    # 单份文档无法比报价
    assert analyze_pricing("task-001", [ir_doc_a]) == []
    # 无表格的文档不参与
    no_table = ir_doc_a.model_copy(update={
        "docId": "doc-x",
        "blocks": [b for b in ir_doc_a.blocks if b.type != "table"],
    })
    assert analyze_pricing("task-001", [ir_doc_a, no_table]) == []


def test_table_without_html_skipped(ir_doc_a):
    # 实测 2/132 表格无 table_html（有截图）：无法解析金额，跳过不参与报价比对
    no_html = ir_doc_a.model_copy(update={
        "docId": "doc-nh",
        "blocks": [
            b.model_copy(update={"table": b.table.model_copy(update={"html": None})})
            if b.type == "table" else b
            for b in ir_doc_a.blocks
        ],
    })
    assert analyze_pricing("task-001", [ir_doc_a, no_html]) == []


def test_malformed_span_table_skipped(ir_doc_a, ir_doc_b):
    """畸形 rowspan="abc" 的表格解析抛 ValueError → 跳过该表，不拖垮整个请求。"""
    bad = ir_doc_a.model_copy(update={
        "docId": "doc-bad",
        "blocks": [
            b.model_copy(update={"table": b.table.model_copy(update={
                "html": '<table><tr><td rowspan="abc">总价</td><td>9,999,999.00</td></tr></table>'
            })})
            if b.type == "table" else b
            for b in ir_doc_a.blocks
        ],
    })
    # bad 的唯一报价表畸形 → 不参与；doc-a/doc-b 正常比对（贴近度 0.99% ≤ 1% 出证据）
    evidences = analyze_pricing("task-001", [bad, ir_doc_a, ir_doc_b])
    assert evidences
    assert all("doc-bad" not in e.docIds for e in evidences)


def test_real_haigang_pair_no_pricing_evidence(raw_haigang_pair):
    """海港1 无表格 → 可报价文档不足 2 份，不出证据（真实数据负路径）。"""
    from app.angineer.adapter import adapt_document
    from app.angineer.raw import validate_raw_document
    docs = [adapt_document(validate_raw_document(d)) for d in raw_haigang_pair]
    assert analyze_pricing("task-real", docs) == []
