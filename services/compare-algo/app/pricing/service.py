"""报价分析证据组装：pricing 类证据，aiGenerated=false。"""
from app.pricing.patterns import (
    detect_arithmetic_progression,
    detect_closeness,
    detect_tail_pattern,
)
from app.pricing.table_parse import extract_total_amount, parse_table_html
from app.schemas.evidence import Evidence, EvidenceLocation, build_evidence
from app.schemas.ir import IrDocument


def _best_price_block(doc: IrDocument) -> tuple[str, float] | None:
    """取文档中投标总价最大的表格块，返回 (blockId, total)。

    无表格、表格无 html（实测 2/132，仅有截图）或无金额时返回 None；
    畸形 html（如 rowspan="abc"）解析抛 ValueError，跳过该表不拖垮整个请求。
    """
    best: tuple[str, float] | None = None
    for block in doc.blocks:
        if block.type != "table" or block.table is None or block.table.html is None:
            continue
        try:
            grid = parse_table_html(block.table.html)
        except ValueError:
            continue
        total = extract_total_amount(grid)
        if total is not None and (best is None or total > best[1]):
            best = (block.blockId, total)
    return best


def analyze_pricing(task_id: str, documents: list[IrDocument]) -> list[Evidence]:
    priced = [(d.docId, bp) for d in documents if (bp := _best_price_block(d)) is not None]
    if len(priced) < 2:
        return []
    doc_ids = [doc_id for doc_id, _ in priced]
    amounts = [bp[1] for _, bp in priced]
    amount_map = {doc_id: bp[1] for doc_id, bp in priced}
    locations = [EvidenceLocation(docId=doc_id, blockIds=[bp[0]]) for doc_id, bp in priced]

    evidences: list[Evidence] = []
    ap = detect_arithmetic_progression(amounts)
    if ap is not None:
        evidences.append(build_evidence(
            task_id=task_id,
            type="pricing",
            severity="high",
            doc_ids=doc_ids,
            locations=locations,
            metrics={"pattern": "arithmetic", "commonDiff": ap.common_diff, "amounts": amount_map},
            title=f"{len(doc_ids)} 份报价呈等差规律（公差约 {ap.common_diff:,.0f} 元）",
            description="多份投标报价构成等差数列，疑似人为排布，属围串标强信号。",
        ))
    tail = detect_tail_pattern(amounts)
    if tail is not None:
        evidences.append(build_evidence(
            task_id=task_id,
            type="pricing",
            severity="mid",
            doc_ids=doc_ids,
            locations=locations,
            metrics={"pattern": "tail", "tail": tail.tail, "amounts": amount_map},
            title=f"{len(doc_ids)} 份报价尾数完全相同（末两位 {tail.tail}）",
            description="多份报价尾数规律一致，疑似同源编制。",
        ))
    close = detect_closeness(amounts)
    if close is not None:
        evidences.append(build_evidence(
            task_id=task_id,
            type="pricing",
            severity="high" if close.spread_ratio <= 0.005 else "mid",
            doc_ids=doc_ids,
            locations=locations,
            metrics={
                "pattern": "closeness",
                "spreadRatio": close.spread_ratio,
                "minAmount": close.min_amount,
                "maxAmount": close.max_amount,
                "amounts": amount_map,
            },
            title=f"{len(doc_ids)} 份报价异常贴近（最大偏差 {close.spread_ratio:.2%}）",
            description="多份报价贴近度异常，疑似协同报价。",
        ))
    return evidences
