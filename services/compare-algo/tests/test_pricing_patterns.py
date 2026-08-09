from app.pricing.patterns import (
    detect_arithmetic_progression,
    detect_closeness,
    detect_tail_pattern,
)


class TestArithmeticProgression:
    def test_exact_ap(self):
        r = detect_arithmetic_progression([1000000, 1020000, 1010000])  # 乱序输入
        assert r is not None
        assert r.amounts == [1000000, 1010000, 1020000]
        assert r.common_diff == 10000

    def test_near_ap_within_tolerance(self):
        assert detect_arithmetic_progression([100, 200, 301]) is not None  # 差 100/101

    def test_not_ap(self):
        assert detect_arithmetic_progression([100, 200, 320]) is None

    def test_two_amounts_not_enough(self):
        assert detect_arithmetic_progression([100, 200]) is None

    def test_equal_amounts_not_ap(self):
        assert detect_arithmetic_progression([100, 100, 100]) is None


class TestTailPattern:
    def test_same_tail(self):
        r = detect_tail_pattern([10067, 20067, 30067])
        assert r is not None
        assert r.tail == "67"

    def test_trivial_zero_tail_excluded(self):
        # 末两位 "00" 是百元取整常态，不构成尾数规律
        assert detect_tail_pattern([10000, 20000, 30000]) is None

    def test_different_tails(self):
        assert detect_tail_pattern([10001, 20002, 30003]) is None

    def test_two_amounts_not_enough(self):
        assert detect_tail_pattern([10000, 20000]) is None


class TestCloseness:
    def test_close(self):
        r = detect_closeness([1000, 1005])
        assert r is not None
        assert r.spread_ratio == round(5 / 1005, 6)

    def test_not_close(self):
        assert detect_closeness([1000, 1100]) is None

    def test_single_amount_not_enough(self):
        assert detect_closeness([1000]) is None
