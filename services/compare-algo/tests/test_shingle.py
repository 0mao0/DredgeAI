from app.similarity.shingle import (
    DEFAULT_NGRAM,
    block_shingles,
    char_ngrams,
    jaccard,
    normalize_text,
)


def test_default_ngram_is_3():
    assert DEFAULT_NGRAM == 3


def test_normalize_text_strips_whitespace_and_punctuation():
    assert normalize_text("我公司 郑重承诺，\n若中标。") == "我公司郑重承诺若中标"
    assert normalize_text("E = mc^2") == "Emc2"
    assert normalize_text("，。！？") == ""


def test_char_ngrams_basic():
    assert char_ngrams("abcd", 3) == {"abc", "bcd"}
    assert char_ngrams("abcde", 2) == {"ab", "bc", "cd", "de"}


def test_char_ngrams_short_text_returns_whole():
    assert char_ngrams("甲乙", 3) == {"甲乙"}
    assert char_ngrams("", 3) == set()


def test_block_shingles_skips_non_text_types(ir_doc_a):
    table_block = next(b for b in ir_doc_a.blocks if b.type == "table")
    assert block_shingles(table_block) == set()
    para = next(b for b in ir_doc_a.blocks if b.blockId == "b002")
    assert block_shingles(para) == char_ngrams(para.text)


def test_block_shingles_skips_furniture():
    # header/footer（页眉页脚页码）不查重：真实数据中不同文档常共享同一规范名页眉，
    # 参与会产生伪雷同（实测海港1/海港2 页眉相同）
    class _B:
        type = "header"
        text = "海港航道设计规范"
    assert block_shingles(_B()) == set()


def test_block_shingles_includes_equation_latex():
    class _B:
        type = "equation"
        text = "E=mc^2"
    assert block_shingles(_B()) == char_ngrams("E=mc^2")


def test_jaccard():
    assert jaccard({"a", "b"}, {"b", "c"}) == 1 / 3
    assert jaccard(set(), set()) == 0.0
    assert jaccard({"a"}, set()) == 0.0
    assert jaccard({"a", "b"}, {"a", "b"}) == 1.0
