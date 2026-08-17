"""局部雷同检测（跨块长公共子串）测试。

覆盖真实场景：语义段相同但块切分不同（A 拆两半、B 一整段）、
片段定位、招标文件响应标记、无招标文件回退、短片段过滤。
"""
from app.angineer.adapter import adapt_document
from app.angineer.raw import validate_raw_document
from app.similarity.passages import (
    MIN_PASSAGE_LEN,
    block_offsets,
    find_common_passages,
    full_text,
    local_similarity_evidences,
)
from app.similarity.service import analyze_similarity
from tests.conftest import adapt, make_raw_block, make_raw_doc

# 模拟「服务承诺」核心句：A/B 逐字相同，块切分不同（A 拆两段、B 单段）
SHARED_PROMISE = (
    "在组织机构人员配备设计质量设计进度相关协调等方面的服务"
    "将以满足业主和项目的第一需要为原则全过程全方位及时高效地做好相关服务工作"
)

# 模拟「企业自写承诺」：A/B 相同但招标文件中不存在（真正的雷同候选）
SHARED_CUSTOM = "我单位将组建由项目负责人牵头的研究服务组开展施工期间设计服务工作"

A_INTRO = "我局系长江口水文水资源勘测局具有丰富的水文测验经验和技术积累"
B_INTRO = "我院系中交上海航道勘察设计研究院有限公司长期承担沿海港口勘察监测项目"
A_OUTRO = "本项目完成后我局将及时提交全部成果资料并配合验收"
B_OUTRO = "本项目完成后我院将按合同约定提交成果并接受专家评审"
# 各自独立的正文：拉低块级整体相似度，确保承诺段只能被局部雷同检出（<0.3 块级阈值）
A_MORE_1 = "本项目水文测验将采用多波束测深系统与单波束互补方式同步采集地形与回淤数据"
A_MORE_2 = "测站布设方案综合考虑了航道轴线水深分布与历年大风天波浪特征确定"
B_MORE_1 = "针对洋山深水港区潮流强含沙量高的特点本项目配置双频测深仪与定点海流计"
B_MORE_2 = "项目组将建立数据质量三级审核机制确保外业测量成果满足规范精度要求"


def _promise_docs(include_tender: bool = False):
    """A 的承诺拆成两段、B 一整段；可选附带招标文件（含 SHARED_PROMISE）。"""
    doc_a = make_raw_doc(
        "doc-a",
        file_name="A公司投标文件.pdf",
        author="张三",
        created_at="2024-01-01",
        blocks=[
            make_raw_block("a1", A_INTRO, page_idx=10),
            make_raw_block("a2", SHARED_PROMISE[:26], page_idx=11),
            make_raw_block("a3", SHARED_PROMISE[26:], page_idx=11),
            make_raw_block("a4", A_MORE_1, page_idx=12),
            make_raw_block("a5", A_MORE_2, page_idx=12),
            make_raw_block("a6", A_OUTRO, page_idx=12),
        ],
    )
    doc_b = make_raw_doc(
        "doc-b",
        file_name="B公司投标文件.pdf",
        author="李四",
        created_at="2024-01-02",
        blocks=[
            make_raw_block("b1", B_INTRO, page_idx=20),
            make_raw_block("b2", SHARED_PROMISE, page_idx=21),
            make_raw_block("b3", B_MORE_1, page_idx=22),
            make_raw_block("b4", B_MORE_2, page_idx=22),
            make_raw_block("b5", B_OUTRO, page_idx=22),
        ],
    )
    docs = [doc_a, doc_b]
    if include_tender:
        tender = make_raw_doc(
            "tender-1",
            file_name="招标文件.pdf",
            author=None,
            created_at=None,
            role="tender",
            blocks=[
                make_raw_block("t1", "招标文件技术要求及服务承诺相关表述", page_idx=1),
                make_raw_block("t2", SHARED_PROMISE, page_idx=2),
            ],
        )
        docs.append(tender)
    return [adapt(d) for d in docs]


def test_common_passage_found_across_different_block_split():
    """A 把承诺拆两段、B 一整段：全文 LCS 仍应检出整句。"""
    docs = _promise_docs()
    ta, tb = full_text(docs[0]), full_text(docs[1])
    passages = find_common_passages(ta, tb, block_offsets(docs[0]), block_offsets(docs[1]))
    assert passages, "应检出至少一个公共片段"
    longest = max(passages, key=lambda p: p[2])
    assert longest[2] >= len(SHARED_PROMISE) - 2, "最长片段应覆盖完整承诺句"
    assert SHARED_PROMISE[:20] in ta[longest[0]:longest[0] + longest[2]]


def test_short_passages_filtered():
    """短于 min_len 的公共片段不检出。"""
    docs = _promise_docs()
    ta, tb = full_text(docs[0]), full_text(docs[1])
    passages = find_common_passages(ta, tb, block_offsets(docs[0]), block_offsets(docs[1]))
    for _, _, length in passages:
        assert length >= MIN_PASSAGE_LEN


def test_passage_block_locations_cover_both_sides():
    """片段定位应覆盖双方实际包含该文本的块。"""
    docs = _promise_docs()
    ta, tb = full_text(docs[0]), full_text(docs[1])
    offsets_a, offsets_b = block_offsets(docs[0]), block_offsets(docs[1])
    passages = find_common_passages(ta, tb, offsets_a, offsets_b)
    a_start, b_start, length = max(passages, key=lambda p: p[2])

    a_blocks = {bid for bid, _, bs, be in offsets_a if be > a_start and bs < a_start + length}
    b_blocks = {bid for bid, _, bs, be in offsets_b if be > b_start and bs < b_start + length}
    # A 侧承诺拆两段（a2/a3）、B 侧单块（b2）：各自偏移定位应覆盖对应块
    assert "a2" in a_blocks and "a3" in a_blocks
    assert "b2" in b_blocks


def test_local_similarity_evidence_with_tender_marking():
    """有招标文件：产出 passage 证据，且招标文件中存在的片段标记 tenderResponse=True。"""
    docs = _promise_docs(include_tender=True)
    evidences = local_similarity_evidences("task-001", docs)
    assert len(evidences) == 1
    e = evidences[0]
    assert e.type == "similarity"
    assert e.metrics["kind"] == "passage"
    assert e.docIds == ["doc-a", "doc-b"]
    assert e.aiGenerated is False

    passages = e.metrics["passages"]
    assert passages, "应产出至少一个片段"
    tender_hit = next(p for p in passages if SHARED_PROMISE[:20] in p["text"])
    assert tender_hit["tenderResponse"] is True, "招标文件含该文本 → 标记为招标响应"


def test_local_similarity_evidence_without_tender():
    """无招标文件：片段照常产出，tenderResponse 为 None（不假设）。"""
    docs = _promise_docs(include_tender=False)
    evidences = local_similarity_evidences("task-001", docs)
    assert len(evidences) == 1
    passages = evidences[0].metrics["passages"]
    assert passages
    assert all("tenderResponse" in p and p["tenderResponse"] is None for p in passages)


def test_analyze_similarity_includes_passage_evidence():
    """analyze_similarity 集成：局部雷同证据与现有矩阵/对证据并存。"""
    docs = _promise_docs(include_tender=False)
    evidences = analyze_similarity("task-001", docs)
    passage = [e for e in evidences if e.metrics.get("kind") == "passage"]
    assert len(passage) == 1
    assert passage[0].metrics["longestLength"] >= len(SHARED_PROMISE) - 2


def test_severity_by_longest_passage():
    """severity 按最长片段分级：≥80 高、≥40 中、≥20 低。"""
    docs = _promise_docs()
    evidences = local_similarity_evidences("task-001", docs)
    e = evidences[0]
    longest = e.metrics["longestLength"]
    assert longest >= 40
    assert e.severity == "mid"
