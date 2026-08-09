"""雷同簇聚类（spec §3.1）：两两高相似经 Union-Find 传递归并，≥3 份的簇单独标记。"""
from app.similarity.align import PairSimilarityResult


def find_similarity_clusters(
    pair_results: list[PairSimilarityResult],
    min_similarity: float = 0.5,
) -> list[list[str]]:
    """返回成员数 >= 3 的雷同簇（簇内 docId 升序）。不足 3 份的归并组不返回。"""
    parent: dict[str, str] = {}

    def find(x: str) -> str:
        parent.setdefault(x, x)
        while parent[x] != x:
            parent[x] = parent[parent[x]]  # 路径压缩
            x = parent[x]
        return x

    def union(a: str, b: str) -> None:
        ra, rb = find(a), find(b)
        if ra != rb:
            parent[rb] = ra

    for r in pair_results:
        if r.similarity >= min_similarity:
            union(r.doc_id_a, r.doc_id_b)

    groups: dict[str, list[str]] = {}
    for doc_id in list(parent):
        groups.setdefault(find(doc_id), []).append(doc_id)
    return sorted(
        (sorted(members) for members in groups.values() if len(members) >= 3),
        key=lambda m: m[0],
    )
