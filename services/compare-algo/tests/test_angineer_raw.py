import json
import pathlib

import pytest
from pydantic import ValidationError

from app.angineer.raw import validate_raw_document

FIXTURES = pathlib.Path(__file__).parent / "fixtures"


def load_fixture(name: str) -> dict:
    return json.loads((FIXTURES / f"{name}.json").read_text(encoding="utf-8"))


def _minimal_raw() -> dict:
    return {
        "docId": "d1",
        "blocks": [
            {
                "block_uid": "d1:0:1",
                "block_type": "paragraph",
                "page_idx": 0,
                "block_seq": 1,
                "plain_text": "正文内容",
                "bbox": [0.04, 0.06, 0.5, 0.1],
                "derived_level": None,
                "parent_uid": None,
                "source": "text",
                "confidence": 1.0,
                "image_path": None,
                "table_html": None,
                "math_content": None,
                "formula_body": None,
                "formula_number": None,
            }
        ],
        "meta": {
            "docMeta": {
                "fileName": "a.pdf",
                "pageCount": 1,
                "author": None,
                "creatorTool": None,
                "createdAt": None,
                "modifiedAt": None,
            },
            "outlines": [],
            "pages": [{"pageIdx": 0, "width": 612.0, "height": 825.0}],
        },
    }


class TestRealFixtures:
    """真实产物必须通过校验（Task 2 裁剪样本）。"""

    @pytest.mark.parametrize("name", ["haigang1", "haigang2", "pingshen_a", "pingshen_b"])
    def test_real_fixture_valid(self, name):
        doc = validate_raw_document(load_fixture(name))
        assert doc.docId.startswith("doc-")
        assert len(doc.blocks) > 0


def test_minimal_raw_passes():
    doc = validate_raw_document(_minimal_raw())
    assert doc.blocks[0].block_uid == "d1:0:1"


def test_unknown_extra_fields_ignored():
    # 产物后续增补字段不破坏契约（实测节点有 content_json/caption_* 等 40+ 字段）
    data = _minimal_raw()
    data["blocks"][0]["content_json"] = {"paragraph_content": []}
    data["blocks"][0]["some_future_field"] = 123
    data["meta"]["edges"] = []
    doc = validate_raw_document(data)
    assert doc.blocks[0].plain_text == "正文内容"


def test_docid_is_opaque_no_prefix_check():
    # docId 为 ABP 文档 id，不要求 block_uid 以其为前缀
    data = _minimal_raw()
    data["docId"] = "abp-guid-0001"
    doc = validate_raw_document(data)
    assert doc.docId == "abp-guid-0001"


def test_missing_block_uid_rejected():
    data = _minimal_raw()
    del data["blocks"][0]["block_uid"]
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_duplicate_block_uid_rejected():
    data = _minimal_raw()
    data["blocks"].append(dict(data["blocks"][0]))
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_pixel_bbox_rejected():
    data = _minimal_raw()
    data["blocks"][0]["bbox"] = [0, 0, 1000, 1000]
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_bbox_wrong_length_rejected_with_clear_message():
    # 3 元素 bbox 不得塌陷为 pydantic "Field required"（tuple 强转先于范围校验）
    data = _minimal_raw()
    data["blocks"][0]["bbox"] = [0.1, 0.2, 0.3]
    with pytest.raises(ValidationError) as exc_info:
        validate_raw_document(data)
    assert any("4 元素" in e["msg"] for e in exc_info.value.errors())


def test_bbox_above_one_or_inverted_rejected():
    for bad in ([0, 0, 1.5, 0.5], [0.5, 0, 0.1, 0.1], [-0.1, 0, 0.5, 0.1]):
        data = _minimal_raw()
        data["blocks"][0]["bbox"] = bad
        with pytest.raises(ValidationError):
            validate_raw_document(data)


def test_null_bbox_accepted():
    # 实测无此情况，但页面尺寸缺失时 AnGIneer 会给 null，宽松接收
    data = _minimal_raw()
    data["blocks"][0]["bbox"] = None
    doc = validate_raw_document(data)
    assert doc.blocks[0].bbox is None


def test_source_vocabulary_enforced():
    # 实测词表 text/ocr/table/formula/null；v2 文档措辞 "native" 拒收
    data = _minimal_raw()
    data["blocks"][0]["source"] = "native"
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_null_source_and_confidence_accepted():
    data = _minimal_raw()
    data["blocks"][0]["source"] = None
    data["blocks"][0]["confidence"] = None
    doc = validate_raw_document(data)
    assert doc.blocks[0].source is None


def test_confidence_out_of_range_rejected():
    data = _minimal_raw()
    data["blocks"][0]["confidence"] = 1.5
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_docmeta_field_must_be_present_even_if_null():
    # v2 §5-7：可 null 不可省略
    data = _minimal_raw()
    del data["meta"]["docMeta"]["author"]
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_pages_required_and_positive_float():
    data = _minimal_raw()
    data["meta"]["pages"] = []
    with pytest.raises(ValidationError):
        validate_raw_document(data)
    data = _minimal_raw()
    data["meta"]["pages"] = [{"pageIdx": 0, "width": 0, "height": 825.0}]
    with pytest.raises(ValidationError):
        validate_raw_document(data)


def test_table_html_purity_enforced():
    for bad_html in (
        '<table class="t"><tr><td>1</td></tr></table>',
        '<table><tr><td style="color:red">1</td></tr></table>',
        '<table><thead><tr><td>1</td></tr></thead></table>',
    ):
        data = _minimal_raw()
        data["blocks"][0]["block_type"] = "table"
        data["blocks"][0]["plain_text"] = ""
        data["blocks"][0]["table_html"] = bad_html
        data["blocks"][0]["image_path"] = "images/t.jpg"
        with pytest.raises(ValidationError):
            validate_raw_document(data)


def test_table_html_with_rowspan_colspan_passes():
    data = _minimal_raw()
    data["blocks"][0]["block_type"] = "table"
    data["blocks"][0]["plain_text"] = ""
    data["blocks"][0]["table_html"] = (
        '<table><tr><td rowspan="2">a</td><td colspan="2">b</td></tr>'
        "<tr><td>c</td><td>d</td></tr></table>"
    )
    data["blocks"][0]["image_path"] = "images/t.jpg"
    doc = validate_raw_document(data)
    assert doc.blocks[0].table_html is not None


def test_empty_table_html_normalized_to_none():
    # AnGIneer 对缺失 html 的表格输出空串，等价于缺省（pricing 会跳过该表）
    data = _minimal_raw()
    data["blocks"][0]["block_type"] = "table"
    data["blocks"][0]["plain_text"] = ""
    data["blocks"][0]["table_html"] = "   "
    data["blocks"][0]["image_path"] = "images/t.jpg"
    doc = validate_raw_document(data)
    assert doc.blocks[0].table_html is None


def test_table_html_with_img_accepted():
    # 实测 AnGIneer 表格单元格内嵌图片（src 指向产物 images/）
    data = _minimal_raw()
    data["blocks"][0]["block_type"] = "table"
    data["blocks"][0]["plain_text"] = ""
    data["blocks"][0]["table_html"] = (
        '<table><tr><td>a</td><td><img src="images/a42.jpg"/></td></tr></table>'
    )
    data["blocks"][0]["image_path"] = "images/t.jpg"
    doc = validate_raw_document(data)
    assert doc.blocks[0].table_html is not None
