"""PDF 原始日期归一：D:YYYYMMDDHHmmSSOHH'mm' → ISO 8601。

实测 docMeta.createdAt 为 PDF 原始日期串（如 D:20251229164720+08'00'）。
已是 ISO 或无法解析的输入原样返回（元数据比对是相等性分组，原样不影响正确性）。
"""
from __future__ import annotations

import re
from datetime import datetime, timedelta, timezone

# 全串锚定：尾部垃圾（如 D:2025abc）整体不匹配 → 原样保留，
# 避免不同垃圾串碰撞出同一日期（Task 14 createdAt 假阳性）；
# 时区分钟可缺省（+08'）：与 +08'00' 同一时刻须产出同一字符串（防假阴性）
_PDF_DATE_RE = re.compile(
    r"^D:(?P<y>\d{4})(?P<mo>\d{2})?(?P<d>\d{2})?"
    r"(?P<h>\d{2})?(?P<mi>\d{2})?(?P<s>\d{2})?"
    r"(?P<tz>[Zz]|[+-]\d{2}'?(?:\d{2}'?)?)?\s*$"
)


def parse_pdf_date(value: str | None) -> str | None:
    if value is None:
        return None
    text = value.strip()
    if not text:
        return None
    m = _PDF_DATE_RE.match(text)
    if not m:
        return value  # ISO 或其他格式：原样保留
    g = m.groupdict()
    try:
        dt = datetime(
            int(g["y"]), int(g["mo"] or 1), int(g["d"] or 1),
            int(g["h"] or 0), int(g["mi"] or 0), int(g["s"] or 0),
        )
    except ValueError:
        return value
    tz = g.get("tz")
    if tz:
        if tz in ("Z", "z"):
            dt = dt.replace(tzinfo=timezone.utc)
        else:
            sign = 1 if tz[0] == "+" else -1
            digits = re.sub(r"\D", "", tz[1:])
            hours = int(digits[:2] or 0)
            minutes = int(digits[2:4] or 0)
            dt = dt.replace(tzinfo=timezone(sign * timedelta(hours=hours, minutes=minutes)))
    return dt.isoformat()
