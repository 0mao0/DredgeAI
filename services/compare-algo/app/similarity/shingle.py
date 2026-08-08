"""段落级 n-gram shingling：中文按字 trigram（tech 决策：bigram/trigram）。

规范化剔除空白与标点，只保留 CJK 表意文字与字母数字，
使排版/换行差异不影响查重。equation 块的 LaTeX 源码同样参与（spec §4.3.6）。
"""
import re

_KEEP_RE = re.compile(r"[一-鿿A-Za-z0-9]+")

# 参与文本查重的块类型；table 由 pricing 域单独处理，header/footer（页眉页脚页码）不查重
# （实测：不同文档常共享同一规范名页眉，参与会产生伪雷同）
SHINGLABLE_TYPES = ("title", "para", "list", "equation")

DEFAULT_NGRAM = 3


def normalize_text(text: str) -> str:
    return "".join(_KEEP_RE.findall(text))


def char_ngrams(text: str, n: int = DEFAULT_NGRAM) -> set[str]:
    """对规范化文本取字级 n-gram；长度不足 n 时整体作为唯一 gram。"""
    norm = normalize_text(text)
    if not norm:
        return set()
    if len(norm) <= n:
        return {norm}
    return {norm[i : i + n] for i in range(len(norm) - n + 1)}


def block_shingles(block, n: int = DEFAULT_NGRAM) -> set[str]:
    """块的 shingle 集合；不参与查重的块类型返回空集。"""
    if block.type not in SHINGLABLE_TYPES:
        return set()
    return char_ngrams(block.text, n)


def jaccard(a: set[str], b: set[str]) -> float:
    union = a | b
    if not union:
        return 0.0
    return len(a & b) / len(union)
