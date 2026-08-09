from app.angineer.adapter import adapt_document
from app.angineer.raw import validate_raw_document
from app.similarity.service import analyze_similarity


def _adapt_all(raw_docs):
    return [adapt_document(validate_raw_document(d)) for d in raw_docs]


def test_fixture_pair_evidence(ir_docs):
    evidences = analyze_similarity("task-001", ir_docs)
    # fixture 中仅 doc-a/doc-b 雷同，doc-c 独立；不足 3 份，无簇证据
    assert len(evidences) == 1
    e = evidences[0]
    assert e.type == "similarity"
    assert e.taskId == "task-001"
    assert e.docIds == ["doc-a", "doc-b"]
    assert e.aiGenerated is False
    # Dice ≈ 0.87 → 本应 high，命中 doc-b b005 低置信 OCR 块 → 降为 mid（spec §4.5）
    assert e.metrics["similarity"] > 0.8
    assert e.metrics["ocrSuspect"] is True
    assert e.severity == "mid"
    assert "扫描件" in e.description


def test_evidence_locations_cover_matched_blocks(ir_docs):
    e = analyze_similarity("task-001", ir_docs)[0]
    loc_a = next(l for l in e.locations if l.docId == "doc-a")
    loc_b = next(l for l in e.locations if l.docId == "doc-b")
    assert "b003" in loc_a.blockIds
    assert "b003" in loc_b.blockIds
    assert "b005" in loc_b.blockIds  # OCR 块也定位出来


def test_no_evidence_for_independent_docs(ir_doc_a, ir_doc_c):
    assert analyze_similarity("task-001", [ir_doc_a, ir_doc_c]) == []


def test_cluster_evidence_for_3_similar_docs(ir_doc_a):
    # 克隆两份（改 docId），构成 3 份完全雷同 → 对证据 + 簇证据
    doc_b = ir_doc_a.model_copy(update={"docId": "doc-b"})
    doc_c = ir_doc_a.model_copy(update={"docId": "doc-c"})
    evidences = analyze_similarity("task-001", [ir_doc_a, doc_b, doc_c])
    pair_evidences = [e for e in evidences if not e.metrics.get("cluster")]
    cluster_evidences = [e for e in evidences if e.metrics.get("cluster")]
    assert len(pair_evidences) == 3  # 3 对，每对 similarity=1.0
    assert all(e.severity == "high" for e in pair_evidences)  # 无 OCR 块，不降权
    assert len(cluster_evidences) == 1
    ce = cluster_evidences[0]
    assert ce.type == "similarity"
    assert ce.severity == "high"
    assert ce.docIds == ["doc-a", "doc-b", "doc-c"]
    assert ce.metrics["memberCount"] == 3
    assert ce.metrics["avgSimilarity"] == 1.0
    assert len(ce.locations) == 3


def test_similarity_thresholds(ir_doc_a):
    # 相似度 < 0.3 不出证据：只保留一个极小重合块无法构造（候选 Jaccard 0.5 已过滤），
    # 这里验证阈值常量存在且单调
    from app.similarity import service
    assert service.SEVERITY_HIGH > service.SEVERITY_MID > service.EVIDENCE_MIN_SIMILARITY > 0


def test_real_haigang_pair_low_evidence(raw_haigang_pair):
    """真实部分雷同对（海港1 vs 海港2）：实测 Dice≈0.346 → 恰好一条 low 证据。

    实测值（2026-08-08 按本计划算法离线演算）：34 个候选块对全部通过单调对齐，
    Dice = 0.3460（≥0.3 出证据，<0.5 为 low）。MinHash/LSH 为近似召回，
    边界块对可能有少量出入，故相似度断言给区间而非精确值。
    """
    docs = _adapt_all(raw_haigang_pair)
    evidences = analyze_similarity("task-real", docs)
    pair_evidences = [e for e in evidences if not e.metrics.get("cluster")]
    assert len(pair_evidences) == 1
    e = pair_evidences[0]
    assert e.docIds == ["doc-12f45ca9", "doc-c8be9f8b"]
    assert 0.3 <= e.metrics["similarity"] < 0.5
    assert e.severity == "low"
    assert e.metrics["ocrSuspect"] is False  # 真实数据 confidence 全 1.0，不降权
    assert e.metrics["matchedBlockCount"] >= 20


def test_real_pingshen_pair_identical(raw_pingshen_pair):
    """评审办法副本对（内容完全一致）：Dice=1.0 → high 证据。"""
    docs = _adapt_all(raw_pingshen_pair)
    evidences = analyze_similarity("task-real", docs)
    assert len(evidences) == 1
    e = evidences[0]
    assert e.metrics["similarity"] == 1.0
    assert e.severity == "high"
