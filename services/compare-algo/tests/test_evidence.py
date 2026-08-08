import pytest
from pydantic import ValidationError

from app.schemas.evidence import Evidence, EvidenceLocation, build_evidence


def _make() -> Evidence:
    return build_evidence(
        task_id="task-001",
        type="similarity",
        severity="high",
        doc_ids=["doc-a", "doc-b"],
        locations=[
            EvidenceLocation(docId="doc-a", blockIds=["b003"]),
            EvidenceLocation(docId="doc-b", blockIds=["b003"]),
        ],
        metrics={"similarity": 0.86},
        title="doc-a 与 doc-b 存在文本雷同",
        description="两段文档存在大段雷同。",
    )


def test_build_evidence_fields_match_spec():
    e = _make()
    assert e.taskId == "task-001"
    assert e.type == "similarity"
    assert e.severity == "high"
    assert e.docIds == ["doc-a", "doc-b"]
    assert e.metrics["similarity"] == 0.86
    # spec §3.2：本服务只产出确定性证据
    assert e.aiGenerated is False
    assert e.id


def test_build_evidence_generates_unique_ids():
    assert _make().id != _make().id


def test_metadata_evidence_may_have_empty_block_ids():
    # 元数据类证据无块级定位，locations 仍按文档给出
    e = build_evidence(
        task_id="task-001",
        type="metadata",
        severity="mid",
        doc_ids=["doc-a", "doc-b"],
        locations=[EvidenceLocation(docId="doc-a"), EvidenceLocation(docId="doc-b")],
        metrics={"field": "author", "value": "张三"},
        title="2 份标书文件作者相同（张三）",
        description="文件元数据作者一致。",
    )
    assert e.locations[0].blockIds == []


def test_invalid_type_rejected():
    with pytest.raises(ValidationError):
        build_evidence(
            task_id="task-001",
            type="unknown",
            severity="high",
            doc_ids=["doc-a"],
            locations=[EvidenceLocation(docId="doc-a")],
            metrics={},
            title="t",
            description="d",
        )


def test_invalid_severity_rejected():
    with pytest.raises(ValidationError):
        build_evidence(
            task_id="task-001",
            type="similarity",
            severity="critical",
            doc_ids=["doc-a"],
            locations=[EvidenceLocation(docId="doc-a")],
            metrics={},
            title="t",
            description="d",
        )


def test_full_type_literal_accepts_clause_and_indicator():
    # spec §6.1 契约含 clause/indicator（由 compare-ai 产出），模型须可表示
    for t in ("clause", "indicator"):
        e = Evidence(
            id="x",
            taskId="task-001",
            type=t,
            severity="low",
            docIds=["doc-a"],
            locations=[EvidenceLocation(docId="doc-a", blockIds=["b001"])],
            metrics={},
            title="t",
            description="d",
            aiGenerated=True,
        )
        assert e.type == t
