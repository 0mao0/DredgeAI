"""报价数值解析与单位归一：千分位、货币符号、「万元/万」单位统一归到「元」。"""
import re

_AMOUNT_RE = re.compile(r"-?\d[\d,]*(?:\.\d+)?")


def parse_amount(raw: str | None) -> float | None:
    """从单元格文本解析金额（单位：元）。无法解析返回 None。

    已知局限：含「万」的非金额文本（如「10万平方米」）也会被 ×10000，
    报价表场景可接受，后续如出现误判再引入列语义判断。
    """
    if raw is None:
        return None
    text = raw.strip()
    if not text:
        return None
    multiplier = 10000.0 if "万" in text else 1.0
    m = _AMOUNT_RE.search(text.replace("￥", "").replace("¥", ""))
    if not m:
        return None
    try:
        value = float(m.group(0).replace(",", ""))
    except ValueError:
        return None
    # round 到分：消除「万」换算浮点伪影（9876.54万 → 98765400.00000001）
    return round(value * multiplier, 2)
