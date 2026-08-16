"""集中配置：全部阈值 / 限额经 pydantic-settings 管理，环境变量前缀 COMPARE_ALGO_。

默认值与各算法模块内的常量保持一致（那些常量保留为「默认值文档」供测试钉住；
运行期一律经 get_settings() 在调用时读取，不在 import 时固化，便于环境变量覆盖
与测试 monkeypatch）。如需重置缓存（如测试改 env 后），调用 get_settings.cache_clear()。
"""
from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict

DEFAULT_MAX_BODY_BYTES = 96 * 1024 * 1024  # 默认 96MB（8 份真实文档产物 ≈ 24MB，留 4× 余量）


class Settings(BaseSettings):
    """运行参数；全部保留原硬编码默认值，环境变量 COMPARE_ALGO_<字段名大写> 覆盖。"""

    model_config = SettingsConfigDict(env_prefix="COMPARE_ALGO_")

    # 请求体大小上限（BodySizeLimitMiddleware，超限 413）
    max_body_bytes: int = DEFAULT_MAX_BODY_BYTES

    # similarity 域阈值
    evidence_min_similarity: float = 0.3   # 低于该值不出证据
    severity_high: float = 0.8
    severity_mid: float = 0.5
    cluster_min_similarity: float = 0.5    # 簇归并阈值

    # MinHash / LSH 粗筛参数
    num_perm: int = 128
    lsh_threshold: float = 0.5       # LSH 召回阈值（近似 Jaccard）
    candidate_jaccard: float = 0.5   # 精确 Jaccard 复核阈值

    # OCR 低置信降权阈值（spec §4.5）
    ocr_low_confidence_threshold: float = 0.5

    # pricing 检测器参数
    arithmetic_rel_tol: float = 0.01   # 等差数列相邻差值相对平均公差的容差
    tail_len: int = 2                  # 尾数规律取整数部分末 N 位
    closeness_max_spread: float = 0.01     # 贴近度触发上限
    closeness_high_spread: float = 0.005   # 贴近度 high/mid 分界（未舍入 spread）

    # 相同错别字检测参数
    typo_ngram: int = 6            # 可疑异常 n-gram 长度
    typo_samples_max: int = 10     # 证据 samples/items 截断上限


@lru_cache
def get_settings() -> Settings:
    """进程级配置单例（lru_cache）；调用时读取，不在模块 import 时固化。"""
    return Settings()
