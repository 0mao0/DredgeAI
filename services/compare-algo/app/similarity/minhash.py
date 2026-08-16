"""MinHash + LSH 粗筛：把 O(N²) 的块对比较降为近邻召回，再用精确 Jaccard 复核。"""
from collections import namedtuple

from datasketch import LeanMinHash, MinHash, MinHashLSH

from app.settings import get_settings
from app.similarity.shingle import DEFAULT_NGRAM, block_shingles, jaccard

NUM_PERM = 128         # 默认值文档；运行期以 settings.num_perm 为准
LSH_THRESHOLD = 0.5      # 默认值文档：LSH 召回阈值（近似 Jaccard）
CANDIDATE_JACCARD = 0.5  # 默认值文档：精确 Jaccard 复核阈值

BlockKey = namedtuple("BlockKey", ["doc_id", "block_id"])
CandidatePair = namedtuple(
    "CandidatePair", ["doc_id_a", "block_id_a", "doc_id_b", "block_id_b", "jaccard"]
)


def build_minhash(shingles: set[str], num_perm: int | None = None) -> LeanMinHash:
    if num_perm is None:
        num_perm = get_settings().num_perm
    mh = MinHash(num_perm=num_perm)
    # update_batch 一次性批量哈希（datasketch>=1.6，内部 numpy 向量化），
    # 与逐条 update 产出的 hashvalues 完全一致，但避免逐 shingle 的 Python 调用开销
    mh.update_batch([s.encode("utf-8") for s in shingles])
    return LeanMinHash(mh)


def build_block_index(documents, n: int = DEFAULT_NGRAM) -> dict:
    """为所有可查重块构建 {BlockKey: (shingles, LeanMinHash)} 索引。"""
    index: dict[BlockKey, tuple[set[str], LeanMinHash]] = {}
    for doc in documents:
        for block in doc.blocks:
            shingles = block_shingles(block, n)
            if shingles:
                index[BlockKey(doc.docId, block.blockId)] = (
                    shingles,
                    build_minhash(shingles),
                )
    return index


_KEY_SEP = "\x1f"  # LSH 键分隔符：不可打印字符，id 含 "/" 时也不会碰撞


def _lsh_key(key: BlockKey) -> str:
    return f"{key.doc_id}{_KEY_SEP}{key.block_id}"


def find_candidate_pairs(
    index: dict,
    threshold: float | None = None,
    min_jaccard: float | None = None,
) -> list[CandidatePair]:
    """LSH 粗筛跨文档候选块对，再用精确 Jaccard 复核。只保留跨文档对。"""
    settings = get_settings()
    if threshold is None:
        threshold = settings.lsh_threshold
    if min_jaccard is None:
        min_jaccard = settings.candidate_jaccard
    lsh = MinHashLSH(threshold=threshold, num_perm=settings.num_perm)
    keys = list(index.keys())
    str_to_key = {_lsh_key(k): k for k in keys}
    with lsh.insertion_session() as session:
        for key in keys:
            session.insert(_lsh_key(key), index[key][1])

    seen: set[tuple[str, str]] = set()
    pairs: list[CandidatePair] = []
    for key in keys:
        skey = _lsh_key(key)
        for other_s in lsh.query(index[key][1]):
            other = str_to_key[other_s]
            if other.doc_id == key.doc_id:
                continue  # 只做跨文档两两比对
            pair_key = tuple(sorted((skey, other_s)))
            if pair_key in seen:
                continue
            seen.add(pair_key)
            jac = jaccard(index[key][0], index[other][0])
            if jac >= min_jaccard:
                pairs.append(
                    CandidatePair(key.doc_id, key.block_id, other.doc_id, other.block_id, round(jac, 4))
                )
    return pairs
