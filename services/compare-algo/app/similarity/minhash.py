"""MinHash + LSH 粗筛：把 O(N²) 的块对比较降为近邻召回，再用精确 Jaccard 复核。"""
from collections import namedtuple

from datasketch import LeanMinHash, MinHash, MinHashLSH

from app.similarity.shingle import DEFAULT_NGRAM, block_shingles, jaccard

NUM_PERM = 128
LSH_THRESHOLD = 0.5      # LSH 召回阈值（近似 Jaccard）
CANDIDATE_JACCARD = 0.5  # 精确 Jaccard 复核阈值

BlockKey = namedtuple("BlockKey", ["doc_id", "block_id"])
CandidatePair = namedtuple(
    "CandidatePair", ["doc_id_a", "block_id_a", "doc_id_b", "block_id_b", "jaccard"]
)


def build_minhash(shingles: set[str], num_perm: int = NUM_PERM) -> LeanMinHash:
    mh = MinHash(num_perm=num_perm)
    for s in shingles:
        mh.update(s.encode("utf-8"))
    return LeanMinHash(mh)


def build_block_index(documents, n: int = DEFAULT_NGRAM, num_perm: int = NUM_PERM) -> dict:
    """为所有可查重块构建 {BlockKey: (shingles, LeanMinHash)} 索引。"""
    index: dict[BlockKey, tuple[set[str], LeanMinHash]] = {}
    for doc in documents:
        for block in doc.blocks:
            shingles = block_shingles(block, n)
            if shingles:
                index[BlockKey(doc.docId, block.blockId)] = (
                    shingles,
                    build_minhash(shingles, num_perm),
                )
    return index


def find_candidate_pairs(
    index: dict,
    threshold: float = LSH_THRESHOLD,
    min_jaccard: float = CANDIDATE_JACCARD,
) -> list[CandidatePair]:
    """LSH 粗筛跨文档候选块对，再用精确 Jaccard 复核。只保留跨文档对。"""
    lsh = MinHashLSH(threshold=threshold, num_perm=NUM_PERM)
    keys = list(index.keys())
    str_to_key = {f"{k.doc_id}/{k.block_id}": k for k in keys}
    with lsh.insertion_session() as session:
        for key in keys:
            session.insert(f"{key.doc_id}/{key.block_id}", index[key][1])

    seen: set[tuple[str, str]] = set()
    pairs: list[CandidatePair] = []
    for key in keys:
        skey = f"{key.doc_id}/{key.block_id}"
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
