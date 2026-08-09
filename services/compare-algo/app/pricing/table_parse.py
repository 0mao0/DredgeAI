"""表格 html 解析：rowspan/colspan 展开为完整网格（占位格填同一值）。

输入 html 已通过产物 schema 纯净度校验（仅 table/tr/td/th），此处不做安全过滤。
"""
from bs4 import BeautifulSoup

from app.pricing.number_norm import parse_amount

_TOTAL_KEYWORDS = ("总价", "合计", "投标报价", "总报价", "总金额")


def parse_table_html(html: str) -> list[list[str]]:
    soup = BeautifulSoup(html, "lxml")
    table = soup.find("table")
    if table is None:
        raise ValueError("table.html 中没有 <table> 标签")
    grid: list[list[str]] = []
    spans: dict[tuple[int, int], str] = {}  # (row, col) -> 被上方 rowspan 占位的值
    for r, tr in enumerate(table.find_all("tr")):
        row: list[str] = []
        col = 0
        for cell in tr.find_all(["td", "th"]):
            while (r, col) in spans:
                row.append(spans.pop((r, col)))
                col += 1
            text = cell.get_text(strip=True)
            rowspan = int(cell.get("rowspan", 1))
            colspan = int(cell.get("colspan", 1))
            for dc in range(colspan):
                row.append(text)
                for dr in range(1, rowspan):
                    spans[(r + dr, col + dc)] = text
            col += colspan
        while (r, col) in spans:  # 行尾被上方 rowspan 占满的格
            row.append(spans.pop((r, col)))
            col += 1
        grid.append(row)
    return grid


def extract_amounts(grid: list[list[str]]) -> list[float]:
    amounts: list[float] = []
    for row in grid:
        for cell in row:
            v = parse_amount(cell)
            if v is not None:
                amounts.append(v)
    return amounts


def extract_total_amount(grid: list[list[str]]) -> float | None:
    """优先取含 总价/合计/报价 关键词行中的最大金额；否则取全表最大金额。

    紧凑型日期（如 20251229）已在 parse_amount 的 token 级排除，两处 max() 均免疫。
    """
    keyword_amounts: list[float] = []
    for row in grid:
        if any(k in "".join(row) for k in _TOTAL_KEYWORDS):
            for cell in row:
                v = parse_amount(cell)
                if v is not None:
                    keyword_amounts.append(v)
    if keyword_amounts:
        return max(keyword_amounts)
    amounts = extract_amounts(grid)
    return max(amounts) if amounts else None
