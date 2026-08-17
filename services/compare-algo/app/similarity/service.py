"""查重证据组装：similarity 类证据，aiGenerated=false。"""
from app.display import display_names
from app.ocr import downgrade_severity, low_confidence_ocr_block_ids
from app.schemas.evidence import Evidence, EvidenceLocation, Severity, build_evidence
from app.schemas.ir import IrDocument
from app.settings import get_settings
from app.similarity.align import PairSimilarityResult, align_document_pair
from app.similarity.cluster import find_similarity_clusters
from app.similarity.minhash import CandidatePair, build_block_index, find_candidate_pairs
from app.similarity.passages import local_similarity_evidences

EVIDENCE_MIN_SIMILARITY = 0.3   # 默认值文档；运行期以 settings 为准（低于该值不出证据）
SEVERITY_HIGH = 0.8             # 默认值文档
SEVERITY_MID = 0.5              # 默认值文档
CLUSTER_MIN_SIMILARITY = 0.5    # 默认值文档（簇归并阈值）


def _severity_of(similarity: float) -> Severity:
    settings = get_settings()
    if similarity >= settings.severity_high:
        return "high"
    if similarity >= settings.severity_mid:
        return "mid"
    return "low"


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


def _matrix_only_evidence(
    task_id: str,
    r: PairSimilarityResult,
    names: dict[str, str],
) -> Evidence:
    """低于雷同证据阈值的两两相似度：仅用于热力矩阵，不进入证据清单（metrics.matrixOnly）。"""
    return build_evidence(
        task_id=task_id,
        type="similarity",
        severity="low",
        doc_ids=[r.doc_id_a, r.doc_id_b],
        locations=[
            EvidenceLocation(docId=r.doc_id_a, blockIds=[]),
            EvidenceLocation(docId=r.doc_id_b, blockIds=[]),
        ],
        metrics={
            "similarity": r.similarity,
            "matrixOnly": True,
            "matchedBlockCount": len(r.matches),
        },
        title=f"{names[r.doc_id_a]} 与 {names[r.doc_id_b]} 文本相似度 {r.similarity:.1%}",
        description="低于雷同证据阈值的两两相似度，用于相似度热力图展示。",
    )


def analyze_similarity(task_id: str, documents: list[IrDocument]) -> list[Evidence]:
    covered_pairs: set[tuple[str, str]] = set()
    evidences = _block_level_evidences(task_id, documents, covered_pairs)
    # 局部雷同是块级查重的补充：块级已报告雷同的对（≥ evidence_min_similarity）
    # 不再重复产出 passage 明细，避免双报；只补块级漏检的局部雷同（如 7% 场景）。
    local_evidences = [
        e for e in local_similarity_evidences(task_id, documents)
        if tuple(sorted(e.docIds)) not in covered_pairs
    ]
    return local_evidences + evidences


def _block_level_evidences(
    task_id: str,
    documents: list[IrDocument],
    covered_pairs: set[tuple[str, str]],
) -> list[Evidence]:
    """块级查重（minhash/LSH + 对齐）与雷同簇；局部雷同由 passages 域单独产出。"""
    settings = get_settings()
    bids = [d for d in documents if d.role != "tender"]
    index = build_block_index(bids)
    pairs = find_candidate_pairs(index)
    by_doc_pair: dict[tuple[str, str], list[CandidatePair]] = {}
    for p in pairs:
        by_doc_pair.setdefault((p.doc_id_a, p.doc_id_b), []).append(p)

    doc_map = {d.docId: d for d in bids}
    ocr_ids = {d.docId: low_confidence_ocr_block_ids(d) for d in bids}
    names = display_names(bids)

    results: list[PairSimilarityResult] = []
    for (a, b), group in sorted(by_doc_pair.items()):
        results.append(align_document_pair(doc_map[a], doc_map[b], group))

    # 全量两两结果：无候选块的对补 0，保证热力矩阵恒有值（含低于证据阈值的低相似度）。
    result_by_pair = {
        tuple(sorted((r.doc_id_a, r.doc_id_b))): r for r in results
    }
    all_results: list[PairSimilarityResult] = []
    for i in range(len(bids)):
        for j in range(i + 1, len(bids)):
            a, b = bids[i].docId, bids[j].docId
            key = tuple(sorted((a, b)))
            all_results.append(result_by_pair.get(key, PairSimilarityResult(a, b, 0.0, [])))

    evidences: list[Evidence] = []
    for r in all_results:
        if r.similarity >= settings.evidence_min_similarity and r.matches:
            covered_pairs.add(tuple(sorted((r.doc_id_a, r.doc_id_b))))
            evidences.append(_pair_evidence(task_id, r, _is_ocr_suspect(r, ocr_ids), names))
        else:
            evidences.append(_matrix_only_evidence(task_id, r, names))

    for members in find_similarity_clusters(results, settings.cluster_min_similarity):
        # 只统计构成归并的边（≥簇阈值）：弱边/非证据对不稀释均值、不进入定位
        sub = [
            r for r in results
            if r.doc_id_a in members
            and r.doc_id_b in members
            and r.similarity >= settings.cluster_min_similarity
            and r.matches
        ]
        if not sub:
            continue  # 防御：簇由 ≥阈值的边归并而来必有边，显式守卫除零
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
