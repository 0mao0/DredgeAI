"""OCR 低置信降权（spec §4.5）。

原生文本中「错得一样」是围标强证据；OCR 文本中可能只是识别器犯了同样的错。
因此 source=ocr 且 confidence<0.5 的块单独标注、证据降一级。
"""
from app.schemas.evidence import Severity
from app.schemas.ir import IrBlock, IrDocument

OCR_LOW_CONFIDENCE_THRESHOLD = 0.5

_SEVERITY_ORDER: list[Severity] = ["low", "mid", "high"]


def is_low_confidence_ocr(block: IrBlock) -> bool:
    # v2 §4：source/confidence 允许 null，缺失时不参与降权（不误伤）
    return (
        block.source == "ocr"
        and block.confidence is not None
        and block.confidence < OCR_LOW_CONFIDENCE_THRESHOLD
    )


def low_confidence_ocr_block_ids(doc: IrDocument) -> set[str]:
    return {b.blockId for b in doc.blocks if is_low_confidence_ocr(b)}


def downgrade_severity(severity: Severity) -> Severity:
    """严重度降一级，low 触底不变。"""
    idx = _SEVERITY_ORDER.index(severity)
    return _SEVERITY_ORDER[max(0, idx - 1)]
