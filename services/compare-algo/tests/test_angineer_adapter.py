import pytest
from pydantic import ValidationError

from app.angineer.pdf_date import parse_pdf_date

from tests.conftest import adapt, make_raw_block, make_raw_doc


class TestPdfDate:
    def test_full_pdf_date(self):
        assert parse_pdf_date("D:20251229164720+08'00'") == "2025-12-29T16:47:20+08:00"

    def test_zulu_timezone(self):
        assert parse_pdf_date("D:20250102030405Z") == "2025-01-02T03:04:05+00:00"

    def test_partial_date_defaults(self):
        assert parse_pdf_date("D:2025") == "2025-01-01T00:00:00"

    def test_iso_passthrough(self):
        assert parse_pdf_date("2026-07-01T10:00:00") == "2026-07-01T10:00:00"

    def test_unparseable_passthrough(self):
        assert parse_pdf_date("not-a-date") == "not-a-date"

    def test_none_and_empty(self):
        assert parse_pdf_date(None) is None
        assert parse_pdf_date("  ") is None


class TestTypeMapping:
    def test_paragraph_and_title(self, ir_doc_a):
        types = {b.blockId: b.type for b in ir_doc_a.blocks}
        assert types["b001"] == "title"
        assert types["b002"] == "para"

    def test_title_text_level_from_derived_level(self, ir_doc_a):
        title = next(b for b in ir_doc_a.blocks if b.type == "title")
        assert title.textLevel == 1
        para = next(b for b in ir_doc_a.blocks if b.type == "para")
        assert para.textLevel == 0

    def test_table_mapping(self, ir_doc_a):
        table = next(b for b in ir_doc_a.blocks if b.type == "table")
        assert table.table is not None
        assert table.table.html.startswith("<table>")
        assert table.table.imgPath == "images/price.jpg"

    def test_equation_uses_math_content(self):
        raw = make_raw_doc("d-eq", file_name="e.pdf", author=None, created_at=None, blocks=[
            make_raw_block("e1", "", block_type="equation_interline",
                           math_content="E=mc^2", image_path="images/e.png"),
        ])
        doc = adapt(raw)
        assert doc.blocks[0].type == "equation"
        assert doc.blocks[0].text == "E=mc^2"

    def test_chart_mapped_to_image(self):
        # 实测 chart：有 image_path、文本为空、source=null（v2 §3 补充映射）
        raw = make_raw_doc("d-chart", file_name="c.pdf", author=None, created_at=None, blocks=[
            make_raw_block("c1", "", block_type="chart", source=None, confidence=None,
                           image_path="images/chart.jpg"),
        ])
        doc = adapt(raw)
        assert doc.blocks[0].type == "image"
        assert doc.blocks[0].imgPath == "images/chart.jpg"

    def test_furniture_mapping(self):
        raw = make_raw_doc("d-f", file_name="f.pdf", author=None, created_at=None, blocks=[
            make_raw_block("h1", "页眉文本", block_type="page_header"),
            make_raw_block("f1", "45", block_type="page_number", seq=2, y=1500),
            make_raw_block("f2", "页脚文本", block_type="page_footer", seq=3, y=1520),
            make_raw_block("p1", "正文", seq=4, y=200),
        ])
        doc = adapt(raw)
        types = {b.blockId: b.type for b in doc.blocks}
        assert types["h1"] == "header"
        assert types["f1"] == "footer"  # page_number 归入 footer（v2 §3：归入或忽略）
        assert types["f2"] == "footer"

    def test_unknown_type_falls_back_to_para(self):
        raw = make_raw_doc("d-u", file_name="u.pdf", author=None, created_at=None, blocks=[
            make_raw_block("u1", "某种未知块内容", block_type="some_future_type"),
        ])
        doc = adapt(raw)
        assert doc.blocks[0].type == "para"
        assert doc.blocks[0].text == "某种未知块内容"


class TestTextSanitize:
    def test_html_tags_stripped(self):
        # 实测标题含 <sub> 等标签
        raw = make_raw_doc("d-h", file_name="h.pdf", author=None, created_at=None, blocks=[
            make_raw_block("t1", "2<sub>.</sub> 1 一般规定", block_type="title", derived_level=2),
        ])
        doc = adapt(raw)
        assert doc.blocks[0].text == "2. 1 一般规定"


class TestMetaMapping:
    def test_pdf_date_normalized(self):
        raw = make_raw_doc("d-m", file_name="m.pdf", author=None,
                           created_at="D:20251229164720+08'00'", blocks=[
                               make_raw_block("p1", "正文"),
                           ])
        doc = adapt(raw)
        assert doc.meta.createdAt == "2025-12-29T16:47:20+08:00"

    def test_iso_date_passthrough(self, ir_doc_a):
        assert ir_doc_a.meta.createdAt == "2026-07-01T10:00:00"


class TestOutlineNesting:
    def test_flat_outlines_to_nested(self):
        raw = make_raw_doc("d-o", file_name="o.pdf", author=None, created_at=None, blocks=[
            make_raw_block("t1", "第一章", block_type="title", derived_level=1),
            make_raw_block("t2", "第一节", block_type="title", derived_level=2, seq=2, y=60),
            make_raw_block("p1", "正文", seq=3, y=120),
        ])
        raw["meta"]["outlines"] = [
            {"outline_id": "o1", "title": "第一章", "level": 1, "page_idx": 0,
             "anchor_block_id": "t1", "parent_outline_id": None, "printed_page_label": "1"},
            {"outline_id": "o2", "title": "第一节", "level": 2, "page_idx": 0,
             "anchor_block_id": "t2", "parent_outline_id": "o1", "printed_page_label": "1"},
        ]
        doc = adapt(raw)
        assert len(doc.outline) == 1
        assert doc.outline[0].blockId == "t1"
        assert doc.outline[0].children[0].blockId == "t2"


class TestInternalModelGuards:
    def test_table_requires_imgpath(self):
        raw = make_raw_doc("d-t", file_name="t.pdf", author=None, created_at=None, blocks=[
            make_raw_block("t1", "", block_type="table",
                           table_html="<table><tr><td>1</td></tr></table>"),
        ])
        with pytest.raises(ValidationError):
            adapt(raw)

    def test_table_without_html_allowed(self):
        # 实测 2/132 表格无 table_html（有整表截图）：合法，pricing 跳过
        raw = make_raw_doc("d-t2", file_name="t.pdf", author=None, created_at=None, blocks=[
            make_raw_block("p1", "正文"),
            make_raw_block("t1", "", block_type="table", seq=2, y=60,
                           image_path="images/t.jpg"),
        ])
        doc = adapt(raw)
        table = next(b for b in doc.blocks if b.type == "table")
        assert table.table is not None
        assert table.table.html is None
        assert table.table.imgPath == "images/t.jpg"

    def test_image_requires_imgpath(self):
        raw = make_raw_doc("d-i", file_name="i.pdf", author=None, created_at=None, blocks=[
            make_raw_block("i1", "", block_type="image"),
        ])
        with pytest.raises(ValidationError):
            adapt(raw)

    def test_equation_requires_latex(self):
        raw = make_raw_doc("d-e", file_name="e.pdf", author=None, created_at=None, blocks=[
            make_raw_block("e1", "", block_type="equation_interline",
                           image_path="images/e.png"),
        ])
        with pytest.raises(ValidationError):
            adapt(raw)

    def test_text_source_confidence_must_be_1(self):
        # source=text（原生文本）confidence 非 null 时必须 1.0
        raw = make_raw_doc("d-s", file_name="s.pdf", author=None, created_at=None, blocks=[
            make_raw_block("p1", "正文", source="text", confidence=0.8),
        ])
        with pytest.raises(ValidationError):
            adapt(raw)

    def test_page_idx_must_exist_in_pages(self):
        raw = make_raw_doc("d-p", file_name="p.pdf", author=None, created_at=None, blocks=[
            make_raw_block("p1", "正文", page_idx=5),
        ])
        with pytest.raises(ValidationError):
            adapt(raw)


class TestRealFixtures:
    """真实产物适配结果固化（实测值来自 Task 2 Step 2）。"""

    def test_haigang1_block_types_and_meta(self, raw_haigang_pair):
        doc = adapt(raw_haigang_pair[0])
        assert doc.docId == "doc-12f45ca9"
        assert len(doc.blocks) == 38
        from collections import Counter
        counts = Counter(b.type for b in doc.blocks)
        assert counts == {"para": 29, "title": 4, "header": 2, "footer": 2, "equation": 1}
        assert doc.meta.creatorTool == "Adobe Acrobat 9.3.2"
        assert doc.meta.createdAt == "2025-12-29T16:47:20+08:00"  # PDF 日期已归一
        assert len(doc.pages) == 2
        assert doc.pages[0].width == 612.0

    def test_haigang1_outline_nested(self, raw_haigang_pair):
        doc = adapt(raw_haigang_pair[0])
        assert len(doc.outline) == 1          # 1 根（第 6 章）
        assert len(doc.outline[0].children) == 3  # 6.1/6.2/6.3
        assert doc.outline[0].level == 1

    def test_pingshen_pair_meta(self, raw_pingshen_pair):
        a, b = (adapt(d) for d in raw_pingshen_pair)
        assert a.meta.author and a.meta.author == b.meta.author
        assert a.meta.creatorTool == b.meta.creatorTool == "Writer"
        assert a.meta.createdAt != b.meta.createdAt  # 实测不同，不应出 createdAt 证据
