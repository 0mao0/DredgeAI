"""报价规律检测：等差 / 尾数 / 贴近度。全部纯函数，输入为各文档投标总价。"""
from dataclasses import dataclass

from app.settings import get_settings


@dataclass
class ArithmeticProgression:
    amounts: list[float]   # 升序
    common_diff: float
    max_deviation: float


def detect_arithmetic_progression(
    amounts: list[float], rel_tol: float | None = None
) -> ArithmeticProgression | None:
    """≥3 份报价构成等差数列：相邻差值相对平均公差的偏差 ≤ rel_tol。"""
    if rel_tol is None:
        rel_tol = get_settings().arithmetic_rel_tol
    if len(amounts) < 3:
        return None
    s = sorted(amounts)
    diffs = [s[i + 1] - s[i] for i in range(len(s) - 1)]
    if any(d <= 0 for d in diffs):
        return None
    avg = sum(diffs) / len(diffs)
    max_dev = max(abs(d - avg) for d in diffs)
    if max_dev / avg <= rel_tol:
        return ArithmeticProgression(amounts=s, common_diff=round(avg, 2), max_deviation=round(max_dev, 2))
    return None


@dataclass
class TailPattern:
    tail: str
    amounts: list[float]   # 升序


def detect_tail_pattern(amounts: list[float], tail_len: int | None = None) -> TailPattern | None:
    """≥3 份报价整数部分末 tail_len 位完全相同（尾数规律，疑似同源编制）。

    末位全 0（如 "00"，百元取整）是报价常态而非规律，排除以免误报。
    """
    if tail_len is None:
        tail_len = get_settings().tail_len
    if len(amounts) < 3:
        return None
    tails = [str(int(a)).zfill(tail_len)[-tail_len:] for a in amounts]
    if len(set(tails)) == 1 and tails[0] != "0" * tail_len:
        return TailPattern(tail=tails[0], amounts=sorted(amounts))
    return None


@dataclass
class Closeness:
    min_amount: float
    max_amount: float
    spread_ratio: float


def detect_closeness(amounts: list[float], max_spread: float | None = None) -> Closeness | None:
    """(max-min)/max ≤ max_spread（默认 1%）视为异常贴近。"""
    if max_spread is None:
        max_spread = get_settings().closeness_max_spread
    if len(amounts) < 2:
        return None
    lo, hi = min(amounts), max(amounts)
    if hi <= 0:
        return None
    spread = (hi - lo) / hi
    if spread <= max_spread:
        return Closeness(min_amount=lo, max_amount=hi, spread_ratio=round(spread, 6))
    return None
