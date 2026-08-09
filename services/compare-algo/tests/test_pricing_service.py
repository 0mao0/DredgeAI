from app.pricing.service import analyze_pricing
from tests.conftest import price_table_html


def _with_price(doc, doc_id, total):
    """复制文档并替换报价表金额 / docId（构造指定价差场景）。"""
    return doc.model_copy(update={
        "docId": doc_id,
        "blocks": [
            b.model_copy(update={"table": b.table.model_copy(update={
                "html": price_table_html(total)
            })})
            if b.type == "table" else b
            for b in doc.blocks
        ],
    })


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
    assert e.metrics["maxDeviation"] == 0
    assert e.metrics["amounts"] == {"doc-a": 1000000.0, "doc-b": 1010000.0, "doc-c": 1020000.0}
    assert e.docIds == ["doc-a", "doc-b", "doc-c"]
    # 标题具名（meta.fileName，、连接），不暴露 opaque docId
    assert e.title == "A公司投标文件.pdf、B公司投标文件.pdf、C公司投标文件.pdf 报价呈等差规律（公差约 10,000 元）"


def test_pricing_locations_point_to_table_blocks(ir_docs):
    e = analyze_pricing("task-001", ir_docs)[0]
    loc = {l.docId: l.blockIds for l in e.locations}
    assert loc == {"doc-a": ["b005"], "doc-b": ["b004"], "doc-c": ["b004"]}


def test_closeness_mid_severity(ir_doc_a, ir_doc_b):
    # 1,000,000 vs 1,010,000：spread 10000/1010000≈0.99% ∈ (0.5%, 1%] → mid
    evidences = analyze_pricing("task-001", [ir_doc_a, ir_doc_b])
    assert len(evidences) == 1
    e = evidences[0]
    assert e.metrics["pattern"] == "closeness"
    assert e.severity == "mid"


def test_closeness_high_severity_under_half_percent(ir_doc_a):
    # 1,000,000 vs 1,004,000：spread 4000/1004000≈0.398% ≤ 0.5% → high
    close_doc = _with_price(ir_doc_a, "doc-close", "1,004,000.00")
    evidences = analyze_pricing("task-001", [ir_doc_a, close_doc])
    assert len(evidences) == 1
    e = evidences[0]
    assert e.metrics["pattern"] == "closeness"
    assert e.severity == "high"


def test_closeness_boundary_half_percent_is_high(ir_doc_a):
    # spread 恰好 0.5%（995,000 vs 1,000,000）：≤ 边界归 high
    boundary_doc = _with_price(ir_doc_a, "doc-bnd", "995,000.00")
    evidences = analyze_pricing("task-001", [ir_doc_a, boundary_doc])
    assert len(evidences) == 1
    e = evidences[0]
    assert e.metrics["pattern"] == "closeness"
    assert e.severity == "high"


def test_closeness_severity_uses_unrounded_spread(ir_doc_a):
    # spread=0.0050004：6dp 舍入后为 0.005（误归 high），未舍入 > 0.5% → mid
    d1 = _with_price(ir_doc_a, "doc-r1", "10,000,000.00")
    d2 = _with_price(ir_doc_a, "doc-r2", "9,949,996.00")
    evidences = analyze_pricing("task-001", [d1, d2])
    assert len(evidences) == 1
    e = evidences[0]
    assert e.metrics["pattern"] == "closeness"
    assert e.metrics["spreadRatio"] == 0.005
    assert e.severity == "mid"


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
    # bad 的唯一报价表畸形 → 不参与；doc-a/doc-b 贴近度 0.99% ∈ (0.5%, 1%] → closeness mid
    evidences = analyze_pricing("task-001", [bad, ir_doc_a, ir_doc_b])
    assert len(evidences) == 1
    e = evidences[0]
    assert e.metrics["pattern"] == "closeness"
    assert e.severity == "mid"
    assert "doc-bad" not in e.docIds


def test_real_haigang_pair_no_pricing_evidence(raw_haigang_pair):
    """海港1 无表格 → 可报价文档不足 2 份，不出证据（真实数据负路径）。"""
    from app.angineer.adapter import adapt_document
    from app.angineer.raw import validate_raw_document
    docs = [adapt_document(validate_raw_document(d)) for d in raw_haigang_pair]
    assert analyze_pricing("task-real", docs) == []
