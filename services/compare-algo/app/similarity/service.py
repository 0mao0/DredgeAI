"""查重证据组装：similarity 类证据，aiGenerated=false。"""
from app.ocr import downgrade_severity, low_confidence_ocr_block_ids
from app.schemas.evidence import Evidence, EvidenceLocation, Severity, build_evidence
from app.schemas.ir import IrDocument
from app.similarity.align import PairSimilarityResult, align_document_pair
from app.similarity.cluster import find_similarity_clusters
from app.similarity.minhash import CandidatePair, build_block_index, find_candidate_pairs

EVIDENCE_MIN_SIMILARITY = 0.3   # 低于该值不出证据
SEVERITY_HIGH = 0.8
SEVERITY_MID = 0.5
CLUSTER_MIN_SIMILARITY = 0.5    # 簇归并阈值


def _severity_of(similarity: float) -> Severity:
    if similarity >= SEVERITY_HIGH:
        return "high"
    if similarity >= SEVERITY_MID:
        return "mid"
    return "low"


def _display_names(documents: list[IrDocument]) -> dict[str, str]:
    """面向用户的文档标识：优先 fileName，缺失/空串时回退 docId（通用约定）。"""
    return {d.docId: (d.meta.fileName or d.docId) for d in documents}


def _is_ocr_suspect(r: PairSimilarityResult, ocr_ids: dict[str, set[str]]) -> bool:
    """任一匹配块命中低置信 OCR 块 → 证据须降权（spec §4.5）。"""
    return any(
        m.block_id_a in ocr_ids[r.doc_id_a] or m.block_id_b in ocr_ids[r.doc_id_b]
        for m in r.matches
    )


def _apply_ocr_downgrade(severity: Severity, suspect: bool) -> tuple[Severity, str]:
    """OCR 降权 + 文案标注；low 触底 severity 不变时，文案不声称「已降权」。"""
    if not suspect:
        return severity, ""
    downgraded = downgrade_severity(severity)
    note = "部分雷同块来自扫描件 OCR 低置信识别，准确率可能受影响。"
    if downgraded != severity:
        note += "已降权处理。"
    return downgraded, note


def _pair_evidence(
    task_id: str,
    r: PairSimilarityResult,
    suspect: bool,
    names: dict[str, str],
) -> Evidence:
    severity, ocr_note = _apply_ocr_downgrade(_severity_of(r.similarity), suspect)
    avg_jac = round(sum(m.jaccard for m in r.matches) / len(r.matches), 4)
    return build_evidence(
        task_id=task_id,
        type="similarity",
        severity=severity,
        doc_ids=[r.doc_id_a, r.doc_id_b],
        locations=[
            EvidenceLocation(docId=r.doc_id_a, blockIds=sorted({m.block_id_a for m in r.matches})),
            EvidenceLocation(docId=r.doc_id_b, blockIds=sorted({m.block_id_b for m in r.matches})),
        ],
        metrics={
            "similarity": r.similarity,
            "avgBlockJaccard": avg_jac,
            "matchedBlockCount": len(r.matches),
            "ocrSuspect": suspect,
        },
        title=f"{names[r.doc_id_a]} 与 {names[r.doc_id_b]} 存在文本雷同（相似度 {r.similarity:.1%}）",
        description="两份文档存在大段雷同。" + ocr_note,
    )


def analyze_similarity(task_id: str, documents: list[IrDocument]) -> list[Evidence]:
    index = build_block_index(documents)
    pairs = find_candidate_pairs(index)
    by_doc_pair: dict[tuple[str, str], list[CandidatePair]] = {}
    for p in pairs:
        by_doc_pair.setdefault((p.doc_id_a, p.doc_id_b), []).append(p)

    doc_map = {d.docId: d for d in documents}
    ocr_ids = {d.docId: low_confidence_ocr_block_ids(d) for d in documents}
    names = _display_names(documents)

    results: list[PairSimilarityResult] = []
    for (a, b), group in sorted(by_doc_pair.items()):
        results.append(align_document_pair(doc_map[a], doc_map[b], group))

    evidences: list[Evidence] = []
    for r in results:
        if r.similarity < EVIDENCE_MIN_SIMILARITY or not r.matches:
            continue
        evidences.append(_pair_evidence(task_id, r, _is_ocr_suspect(r, ocr_ids), names))

    for members in find_similarity_clusters(results, CLUSTER_MIN_SIMILARITY):
        # 只统计构成归并的边（≥簇阈值）：弱边/非证据对不稀释均值、不进入定位
        sub = [
            r for r in results
            if r.doc_id_a in members
            and r.doc_id_b in members
            and r.similarity >= CLUSTER_MIN_SIMILARITY
            and r.matches
        ]
        avg_sim = round(sum(r.similarity for r in sub) / len(sub), 4)
        suspect = any(_is_ocr_suspect(r, ocr_ids) for r in sub)
        severity, ocr_note = _apply_ocr_downgrade("high", suspect)
        locations: list[EvidenceLocation] = []
        for m in members:
            block_ids = sorted(
                {mm.block_id_a for r in sub if r.doc_id_a == m for mm in r.matches}
                | {mm.block_id_b for r in sub if r.doc_id_b == m for mm in r.matches}
            )
            if block_ids:
                locations.append(EvidenceLocation(docId=m, blockIds=block_ids))
        evidences.append(build_evidence(
            task_id=task_id,
            type="similarity",
            severity=severity,
            doc_ids=members,
            locations=locations,
            metrics={
                "cluster": True,
                "memberCount": len(members),
                "avgSimilarity": avg_sim,
                "ocrSuspect": suspect,
            },
            title=f"{len(members)} 份标书存在共同雷同（{', '.join(names[m] for m in members)}）",
            description="≥3 份标书经两两高相似传递归并构成雷同簇，是围串标强信号。" + ocr_note,
        ))
    return evidences
