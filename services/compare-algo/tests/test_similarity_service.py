from app.angineer.adapter import adapt_document
from app.angineer.raw import validate_raw_document
from app.similarity.align import BlockMatch, PairSimilarityResult
from app.similarity.service import analyze_similarity
from tests.conftest import adapt, make_raw_block, make_raw_doc


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
    # severity 实际降了一级（high→mid），文案须声称「已降权处理」
    assert "已降权处理" in e.description
    # 标题面向用户：使用 fileName 而非 opaque docId（通用约定）
    assert "A公司投标文件.pdf" in e.title
    assert "B公司投标文件.pdf" in e.title
    assert "doc-a" not in e.title


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
    assert ce.metrics["ocrSuspect"] is False
    assert len(ce.locations) == 3
    # 簇标题同样使用 fileName
    assert "A公司投标文件.pdf" in ce.title
    assert "doc-a" not in ce.title


def test_similarity_thresholds(monkeypatch, ir_docs):
    # 常量单调性钉
    from app.similarity import service
    assert service.SEVERITY_HIGH > service.SEVERITY_MID > service.EVIDENCE_MIN_SIMILARITY > 0

    # 0.3 出证据 cutoff 的行为钉：monkeypatch 对齐层返回合成相似度（0.3 边界的
    # 低相似场景无法经 LSH 候选真实构造——候选 Jaccard 0.5 已过滤，故钉在 service 边界）
    def fake_align(sim: float):
        def _fake(doc_a, doc_b, pairs):
            return PairSimilarityResult(
                doc_a.docId,
                doc_b.docId,
                sim,
                [BlockMatch(p.block_id_a, p.block_id_b, p.jaccard) for p in pairs],
            )
        return _fake

    monkeypatch.setattr(service, "align_document_pair", fake_align(0.29))
    assert analyze_similarity("task-threshold", ir_docs) == []  # <0.3 不出证据

    monkeypatch.setattr(service, "align_document_pair", fake_align(0.31))
    evidences = analyze_similarity("task-threshold", ir_docs)
    assert len(evidences) == 1  # ≥0.3 出证据
    assert evidences[0].metrics["similarity"] == 0.31
    assert evidences[0].severity == "low"


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
    assert e.metrics["similarity"] == 0.3469  # 当前实现实测固化值（区间内的精确钉）
    assert e.severity == "low"
    assert e.metrics["ocrSuspect"] is False  # 真实数据 confidence 全 1.0，不降权
    assert e.metrics["matchedBlockCount"] >= 20
    # 标题使用真实 fileName（海港1.pdf / 海港2.pdf），不暴露 opaque docId
    assert "海港1.pdf" in e.title
    assert "海港2.pdf" in e.title
    assert "doc-12f45ca9" not in e.title


def test_real_pingshen_pair_identical(raw_pingshen_pair):
    """评审办法副本对（内容完全一致）：Dice=1.0 → high 证据。"""
    docs = _adapt_all(raw_pingshen_pair)
    evidences = analyze_similarity("task-real", docs)
    assert len(evidences) == 1
    e = evidences[0]
    assert e.metrics["similarity"] == 1.0
    assert e.severity == "high"


def test_analyze_similarity_deterministic(raw_haigang_pair):
    """同一输入两次运行产出完全一致（除自动生成的 id）——LSH/排序无隐式随机性。"""
    docs = _adapt_all(raw_haigang_pair)
    first = analyze_similarity("task-real", docs)
    second = analyze_similarity("task-real", docs)
    assert [e.model_dump(exclude={"id"}) for e in first] == [
        e.model_dump(exclude={"id"}) for e in second
    ]


def test_title_falls_back_to_doc_id_when_filename_missing():
    para = "本工程采用框架剪力墙结构体系抗震设防烈度为七度"
    raw_x = make_raw_doc("doc-x", file_name=None, author=None, created_at=None,
                         blocks=[make_raw_block("x-b01", para)])
    raw_y = make_raw_doc("doc-y", file_name=None, author=None, created_at=None,
                         blocks=[make_raw_block("y-b01", para)])
    e = analyze_similarity("task-fallback", [adapt(raw_x), adapt(raw_y)])[0]
    assert e.title == "doc-x 与 doc-y 存在文本雷同（相似度 100.0%）"


# --- 链式簇场景：A-B / B-C 为 ≥0.5 归并边，A-C 仅共享一块（弱边 <0.5，不稀释簇） ---
_S = [
    "本工程采用框架剪力墙结构体系抗震设防烈度为七度",
    "施工组织设计包含进度计划资源配置与安全保证措施",
    "模板工程选用覆膜木胶合板支撑体系采用盘扣式脚手架",
    "混凝土浇筑采用商品混凝土泵送入模分层振捣密实",
    "施工现场临时用电执行三级配电两级保护标准",
]
_T = [
    "质量保证体系覆盖原材料进场检验试验与工序交接检查验收",
    "雨季施工安排包括基坑抽排水材料防雨防潮与边坡防护措施",
    "塔吊选型满足最大构件吊装重量与最大作业半径覆盖要求",
    "文明施工承诺包含围挡全封闭扬尘在线控制与噪声管理",
    "竣工验收资料按城建档案馆归档要求同步整理组卷移交",
]
_W_CHAIN = "本承诺书有效期与投标有效期一致"
_UA = [
    "我司近年完成同类市政道路工程业绩六项",
    "项目班子配备一级建造师两名与技术负责人一名",
    "拟投入履带挖掘机两台压路机三台摊铺机一台",
    "农民工工资按月足额发放",
]
_UC = [
    "智慧工地平台实现人员定位与视频监控全覆盖",
    "绿色施工执行四节一环保评价标准各项指标",
    "深基坑支护采用排桩加内支撑联合支护形式",
    "bim技术应用于管线综合排布与碰撞检查",
]


def _make_doc(doc_id: str, file_name: str, texts: list[str]):
    return make_raw_doc(
        doc_id,
        file_name=file_name,
        author=None,
        created_at=None,
        blocks=[make_raw_block(f"{doc_id}-b{i:02d}", text, seq=i)
                for i, text in enumerate(texts, 1)],
    )


def test_cluster_avg_and_locations_over_union_edges_only():
    # A 共享 S 给 B，C 共享 T 给 B；A-C 仅共享一块 W（弱边）
    docs = [
        adapt(_make_doc("doc-a", "A.pdf", _S + [_W_CHAIN] + _UA)),
        adapt(_make_doc("doc-b", "B.pdf", _S + _T)),
        adapt(_make_doc("doc-c", "C.pdf", _T + [_W_CHAIN] + _UC)),
    ]
    evidences = analyze_similarity("task-chain", docs)
    pair_evidences = [e for e in evidences if not e.metrics.get("cluster")]
    cluster_evidences = [e for e in evidences if e.metrics.get("cluster")]
    # A-C 弱边（<0.3）不出对证据
    assert sorted(e.docIds for e in pair_evidences) == [["doc-a", "doc-b"], ["doc-b", "doc-c"]]
    assert len(cluster_evidences) == 1
    ce = cluster_evidences[0]
    assert ce.docIds == ["doc-a", "doc-b", "doc-c"]
    sims = {tuple(e.docIds): e.metrics["similarity"] for e in pair_evidences}
    assert sims[("doc-a", "doc-b")] >= 0.5
    assert sims[("doc-b", "doc-c")] >= 0.5
    # 均值只取构成归并的边（A-B、B-C），不含 A-C 弱边
    expected = round((sims[("doc-a", "doc-b")] + sims[("doc-b", "doc-c")]) / 2, 4)
    assert ce.metrics["avgSimilarity"] == expected
    # 弱边块不进入簇定位
    loc_a = next(l for l in ce.locations if l.docId == "doc-a")
    loc_c = next(l for l in ce.locations if l.docId == "doc-c")
    assert loc_a.blockIds == [f"doc-a-b0{i}" for i in range(1, 6)]
    assert loc_c.blockIds == [f"doc-c-b0{i}" for i in range(1, 6)]
    assert "doc-a-b06" not in loc_a.blockIds  # W 块只属于 A-C 弱边
    assert "doc-c-b06" not in loc_c.blockIds


def test_cluster_ocr_downgrade(ir_doc_b):
    # 3 份完全雷同且匹配块命中低置信 OCR → 对证据与簇证据都降一级
    docs = [ir_doc_b] + [
        ir_doc_b.model_copy(update={"docId": d}) for d in ("doc-b2", "doc-b3")
    ]
    evidences = analyze_similarity("task-ocr", docs)
    pair_evidences = [e for e in evidences if not e.metrics.get("cluster")]
    cluster_evidences = [e for e in evidences if e.metrics.get("cluster")]
    assert len(pair_evidences) == 3
    assert all(e.metrics["similarity"] == 1.0 for e in pair_evidences)
    assert all(e.severity == "mid" for e in pair_evidences)  # high 降一级
    assert all(e.metrics["ocrSuspect"] is True for e in pair_evidences)
    assert len(cluster_evidences) == 1
    ce = cluster_evidences[0]
    assert ce.docIds == ["doc-b", "doc-b2", "doc-b3"]
    assert ce.severity == "mid"  # 簇证据同样降权，不再硬编码 high
    assert ce.metrics["ocrSuspect"] is True
    assert ce.metrics["memberCount"] == 3
    assert ce.metrics["avgSimilarity"] == 1.0
    assert "扫描件" in ce.description
    assert "已降权处理" in ce.description
    assert len(ce.locations) == 3


# --- low 触底场景：OCR 降权为 no-op，文案不得声称「已降权」 ---
_W_LONG = (
    "若我方中标将在合同签订后三十个日历日内组织人员机械进场施工"
    "严格按照招标文件要求的质量标准与安全规范组织实施"
    "并按规定缴纳履约保证金确保工程按期保质保量完成特此郑重承诺"
)
_UX = [
    "我司具备市政公用工程施工总承包一级资质近五年完成同类道路桥梁工程业绩十余项履约信誉良好",
    "本项目拟投入履带式挖掘机四台振动压路机六台沥青摊铺机两台均经检测合格配备专职操作人员",
    "项目部实行工程款专户专用管理制度农民工工资按月足额代发设立维权公示牌接受社会监督",
]
_UY = [
    "智慧工地管理平台实现人员实名制定位视频远程监控扬尘噪声在线监测与塔吊黑匣子数据覆盖",
    "绿色施工严格执行四节一环保评价标准建筑垃圾分类收集清运现场裸土全覆盖自动喷淋降尘",
    "深基坑工程采用排桩加预应力锚索联合支护形式开挖过程实施信息化监测变形数据日报送监理",
]


def test_low_severity_ocr_note_does_not_claim_downgrade():
    # 唯一匹配块是低置信 OCR 块，相似度落在 [0.3, 0.5) → low 触底，降权为 no-op
    raw_x = _make_doc("doc-x", "X.pdf", [_W_LONG] + _UX)
    raw_x["blocks"][0]["source"] = "ocr"
    raw_x["blocks"][0]["confidence"] = 0.3
    raw_y = _make_doc("doc-y", "Y.pdf", [_W_LONG] + _UY)
    evidences = analyze_similarity("task-low", [adapt(raw_x), adapt(raw_y)])
    assert len(evidences) == 1
    e = evidences[0]
    assert 0.3 <= e.metrics["similarity"] < 0.5
    assert e.severity == "low"  # low 触底不再降
    assert e.metrics["ocrSuspect"] is True
    assert "扫描件" in e.description  # 仍标注 OCR 风险
    assert "已降权处理" not in e.description  # 但未实际降权，不谎称
