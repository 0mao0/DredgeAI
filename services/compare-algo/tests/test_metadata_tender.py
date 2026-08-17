"""错别字/异常串检测的招标文件比对接入测试。

有招标文件时：候选异常串命中招标文件 → 视为「招标响应/模板」排除（误报降权）；
未命中 → 保留为「雷同候选」（tenderResponse=false）。无招标文件时行为不变。
"""
from app.angineer.adapter import adapt_document
from app.angineer.raw import validate_raw_document
from app.metadata.service import analyze_metadata
from tests.conftest import make_raw_block, make_raw_doc

A_INTRO = "我司系市政一级资质施工企业。"
B_INTRO = "本单位具备建筑工程总承包特级资质。"
# 含故意错别字「保证今」（保证金→保证今），A/B 共享 → typo 碰撞
SHARED = (
    "我公司郑重承诺，若我方中标，将在合同签订后三十个日历日内进场施工，"
    "严格按照招标文件要求的质量标准组织实施，并缴纳履约保证今壹佰万元整，"
    "确保工程按期保质完成，特此承诺。"
)


def _docs(tender_text: str | None = None):
    docs = [
        make_raw_doc("doc-a", file_name="A公司投标文件.pdf", author=None, created_at=None, blocks=[
            make_raw_block("a1", A_INTRO),
            make_raw_block("a2", SHARED),
        ]),
        make_raw_doc("doc-b", file_name="B公司投标文件.pdf", author=None, created_at=None, blocks=[
            make_raw_block("b1", B_INTRO),
            make_raw_block("b2", SHARED),
        ]),
    ]
    if tender_text is not None:
        docs.append(make_raw_doc("tender-1", file_name="招标文件.pdf", author=None, created_at=None, role="tender", blocks=[
            make_raw_block("t1", tender_text),
        ]))
    return [adapt_document(validate_raw_document(d)) for d in docs]


def _typo_evidences(evidences):
    return [e for e in evidences if e.metrics.get("pattern") == "shared-typo"]


def test_typo_without_tender_keeps_evidence():
    """无招标文件：维持现状，items 不带 tenderResponse。"""
    evs = analyze_metadata("task-001", _docs())
    typos = _typo_evidences(evs)
    assert len(typos) == 1
    assert all("tenderResponse" not in item for item in typos[0].metrics["items"])


def test_typo_hit_in_tender_excluded():
    """候选串全部命中招标文件 → 视为招标响应/模板，不出错别字证据（误报降权）。"""
    evs = analyze_metadata("task-001", _docs(tender_text=SHARED))
    assert _typo_evidences(evs) == []


def test_typo_not_in_tender_kept_as_candidate():
    """候选串未命中招标文件 → 保留为雷同候选，item.tenderResponse=false。"""
    evs = analyze_metadata("task-001", _docs(tender_text="招标文件其他内容，不包含该承诺文本。"))
    typos = _typo_evidences(evs)
    assert len(typos) == 1
    items = typos[0].metrics["items"]
    assert items
    assert all(item["tenderResponse"] is False for item in items)
    assert typos[0].metrics["tenderResponseCount"] == 0
