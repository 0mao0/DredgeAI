from app.similarity.align import PairSimilarityResult
from app.similarity.cluster import find_similarity_clusters


def _r(a: str, b: str, sim: float) -> PairSimilarityResult:
    return PairSimilarityResult(a, b, sim, [])


def test_cluster_of_4_via_transitive_merge():
    results = [_r("A", "B", 0.9), _r("B", "C", 0.8), _r("C", "D", 0.7)]
    assert find_similarity_clusters(results) == [["A", "B", "C", "D"]]


def test_pair_only_not_a_cluster():
    results = [_r("A", "B", 0.9)]
    assert find_similarity_clusters(results) == []


def test_below_threshold_not_merged():
    results = [_r("A", "B", 0.9), _r("B", "C", 0.3)]  # 0.3 < 默认 0.5
    assert find_similarity_clusters(results) == []


def test_two_independent_clusters():
    results = [_r("A", "B", 0.9), _r("B", "C", 0.9), _r("X", "Y", 0.8), _r("Y", "Z", 0.8)]
    clusters = find_similarity_clusters(results)
    assert sorted(clusters) == [["A", "B", "C"], ["X", "Y", "Z"]]


def test_custom_threshold():
    results = [_r("A", "B", 0.6), _r("B", "C", 0.6)]
    assert find_similarity_clusters(results, min_similarity=0.7) == []
    assert find_similarity_clusters(results, min_similarity=0.6) == [["A", "B", "C"]]
