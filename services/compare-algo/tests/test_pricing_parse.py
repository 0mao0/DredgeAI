import pytest

from app.pricing.number_norm import parse_amount
from app.pricing.table_parse import extract_amounts, extract_total_amount, parse_table_html


class TestParseAmount:
    def test_plain_number(self):
        assert parse_amount("1000000") == 1000000.0

    def test_thousands_separator(self):
        assert parse_amount("1,000,000.00") == 1000000.0

    def test_currency_symbol(self):
        assert parse_amount("￥1,000,000.00") == 1000000.0
        assert parse_amount("¥500") == 500.0

    def test_wan_unit_normalized_to_yuan(self):
        assert parse_amount("100万元") == 1000000.0
        assert parse_amount("100万") == 1000000.0
        assert parse_amount("3.5万元") == 35000.0

    def test_wan_float_artifact_rounded(self):
        # 9876.54 * 10000 浮点伪影须归一到分（2 位小数）
        assert parse_amount("9876.54万") == 98765400.0

    def test_embedded_in_label(self):
        assert parse_amount("小写：1,000,000.00 元") == 1000000.0

    def test_unparseable_returns_none(self):
        assert parse_amount("") is None
        assert parse_amount("无报价") is None
        assert parse_amount(None) is None

    def test_compact_date_token_rejected(self):
        # 8 位紧凑日期 token（19/20 开头，如 20251229）不识别为金额，含嵌入文本场景
        assert parse_amount("20251229") is None
        assert parse_amount("开标日期：20251229") is None


class TestParseTableHtml:
    def test_simple_grid(self):
        grid = parse_table_html("<table><tr><td>a</td><td>b</td></tr><tr><td>c</td><td>d</td></tr></table>")
        assert grid == [["a", "b"], ["c", "d"]]

    def test_rowspan_expanded_with_same_value(self):
        grid = parse_table_html(
            '<table><tr><td rowspan="2">a</td><td>b</td></tr><tr><td>c</td></tr></table>'
        )
        assert grid == [["a", "b"], ["a", "c"]]

    def test_colspan_expanded(self):
        grid = parse_table_html(
            '<table><tr><td colspan="2">a</td></tr><tr><td>b</td><td>c</td></tr></table>'
        )
        assert grid == [["a", "a"], ["b", "c"]]

    def test_rowspan_plus_colspan(self):
        grid = parse_table_html(
            '<table><tr><td rowspan="2" colspan="2">x</td><td>b</td></tr><tr><td>c</td></tr></table>'
        )
        assert grid == [["x", "x", "b"], ["x", "x", "c"]]

    def test_no_table_tag_raises(self):
        with pytest.raises(ValueError):
            parse_table_html("<div>not a table</div>")


class TestExtractTotalAmount:
    def test_keyword_row_preferred(self):
        grid = [
            ["项目", "金额"],
            ["分部分项工程费", "800,000.00"],
            ["投标总价（元）", "1,000,000.00"],
        ]
        assert extract_total_amount(grid) == 1000000.0

    def test_fallback_to_max_amount(self):
        grid = [["单价", "100"], ["数量", "5"], ["小计", "500"]]
        assert extract_total_amount(grid) == 500.0

    def test_no_amounts_returns_none(self):
        assert extract_total_amount([["项目", "说明"], ["工期", "一年"]]) is None

    def test_fallback_ignores_compact_date(self):
        # 无关键词行时，紧凑型日期（8 位裸数字，如 20251229）不得作为总价候选
        grid = [["日期", "20251229"], ["金额", "500000"]]
        assert extract_total_amount(grid) == 500000.0

    def test_fallback_only_compact_date_returns_none(self):
        assert extract_total_amount([["日期", "20251229"]]) is None

    def test_fallback_embedded_compact_date_ignored(self):
        # 嵌入文本中的紧凑日期同样不得作为 fallback 总价候选
        grid = [["开标日期：20251229"], ["金额", "500000"]]
        assert extract_total_amount(grid) == 500000.0

    def test_keyword_row_embedded_compact_date_ignored(self):
        # 关键词行内嵌入紧凑日期：不得压过同行的真实报价
        grid = [["投标总价（开标日期：20251229）", "500,000.00"]]
        assert extract_total_amount(grid) == 500000.0

    def test_extract_amounts_collects_all(self):
        grid = [["a", "100"], ["200", "c"]]
        assert extract_amounts(grid) == [100.0, 200.0]


def test_fixture_price_tables(ir_doc_a):
    table_block = next(b for b in ir_doc_a.blocks if b.type == "table")
    grid = parse_table_html(table_block.table.html)
    assert extract_total_amount(grid) == 1000000.0


def test_real_fixture_table_parse(raw_haigang_pair):
    """真实表格 html 可解析（海港2 含多张纯净表格）。"""
    from app.angineer.adapter import adapt_document
    from app.angineer.raw import validate_raw_document
    doc = adapt_document(validate_raw_document(raw_haigang_pair[1]))
    tables = [b for b in doc.blocks if b.type == "table" and b.table and b.table.html]
    assert len(tables) > 0
    for t in tables:
        grid = parse_table_html(t.table.html)
        assert all(isinstance(row, list) for row in grid)
