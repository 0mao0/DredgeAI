from app.ocr import (
    OCR_LOW_CONFIDENCE_THRESHOLD,
    downgrade_severity,
    is_low_confidence_ocr,
    low_confidence_ocr_block_ids,
)


def test_threshold_is_0_5():
    # spec §4.5：confidence<0.5 降权
    assert OCR_LOW_CONFIDENCE_THRESHOLD == 0.5


def test_is_low_confidence_ocr(ir_doc_a, ir_doc_b):
    a_b4 = next(b for b in ir_doc_a.blocks if b.blockId == "b004")
    b_b5 = next(b for b in ir_doc_b.blocks if b.blockId == "b005")
    b_b3 = next(b for b in ir_doc_b.blocks if b.blockId == "b003")
    assert is_low_confidence_ocr(a_b4) is False      # source=text（原生）
    assert is_low_confidence_ocr(b_b5) is True       # ocr + 0.3
    assert is_low_confidence_ocr(b_b3) is False      # source=text（原生）


def test_confidence_exactly_0_5_not_low(ir_doc_b):
    # 严格 < 边界：confidence 恰好 0.5 不算低置信
    b_b5 = next(b for b in ir_doc_b.blocks if b.blockId == "b005")
    block = b_b5.model_copy(update={"confidence": 0.5})
    assert block.confidence == 0.5
    assert is_low_confidence_ocr(block) is False


def test_non_ocr_source_not_low_even_with_low_confidence(ir_doc_b):
    # ocr-only 闸门：table/formula 来源即使 confidence 低也不参与降权
    b_b5 = next(b for b in ir_doc_b.blocks if b.blockId == "b005")
    for source in ("table", "formula"):
        block = b_b5.model_copy(update={"source": source, "confidence": 0.3})
        assert is_low_confidence_ocr(block) is False


def test_null_source_confidence_block_not_low(ir_doc_a):
    # v2 §4：source/confidence 为 null 的块不参与降权（AnGIneer 补齐前降级关闭）
    block = ir_doc_a.blocks[0].model_copy(update={"source": None, "confidence": None})
    assert is_low_confidence_ocr(block) is False


def test_low_confidence_ocr_block_ids(ir_doc_b):
    assert low_confidence_ocr_block_ids(ir_doc_b) == {"b005"}


def test_downgrade_severity():
    assert downgrade_severity("high") == "mid"
    assert downgrade_severity("mid") == "low"
    assert downgrade_severity("low") == "low"  # 触底不再降
