"""测试 fixture。

合成 fixture：3 份虚拟标书，以 **AnGIneer 原始产物形态**（snake_case）构造，
经 validate_raw_document + adapt_document 得到内部模型——全链路走适配层，
与生产路径一致（Task 16 的 ir_payload 直接复用 raw 形态）。

- doc-a / doc-b：共享一段雷同承诺文本（内含故意错别字「保证今」，各出现一次）、
  同一作者「张三」、同一 createdAt、报价 1,000,000 / 1,010,000（与 doc-c 构成等差）。
- doc-b 另有一个 source=ocr 且 confidence=0.3 的低置信块，文本与 doc-a 的 b004 完全相同，
  用于 OCR 降权测试（spec §4.5；真实数据 confidence 全 1.0，此场景只能靠合成）。
- doc-c：内容、作者、时间均独立，报价 1,020,000（三份构成公差 10,000 的等差数列）。
- 三份 creatorTool 均为 "Microsoft Word"（全相同 → 低危线索）。

真实 fixture：tests/fixtures/ 下 4 份裁剪产物（Task 2 生成），
raw_haigang_pair（部分雷同对）/ raw_pingshen_pair（元数据一致对）。
"""
import json
import pathlib

import pytest

from app.angineer.adapter import adapt_document
from app.angineer.raw import validate_raw_document
from app.schemas.ir import IrDocument

FIXTURES = pathlib.Path(__file__).parent / "fixtures"

PAGE = {"pageIdx": 0, "width": 1190.0, "height": 1684.0}

SHARED_PARAGRAPH = (
    "我公司郑重承诺，若我方中标，将在合同签订后三十个日历日内进场施工，"
    "严格按照招标文件要求的质量标准组织实施，并缴纳履约保证今壹佰万元整，"
    "确保工程按期保质完成，特此承诺。"
)
# 注：「保证今」为故意植入的错别字（金→今），A/B 各出现一次，全文仅此一次。

A_INTRO = "我司系市政一级资质施工企业。"
B_INTRO = "本单位具备建筑工程总承包特级资质。"
A_SEAL_LINE = "本承诺书由法定代表人签字并加盖公章后生效。"

C_TITLE = "技术方案响应"
C_PARA_1 = "本集团致力于智慧园区整体解决方案的研发与交付。"
C_PARA_2 = "我们拥有完全自主的知识产权与成熟实施经验。"


def make_raw_block(
    uid: str,
    text: str,
    *,
    block_type: str = "paragraph",
    source: str | None = "text",
    confidence: float | None = 1.0,
    page_idx: int = 0,
    seq: int = 1,
    y: int = 0,
    derived_level: int | None = None,
    table_html: str | None = None,
    image_path: str | None = None,
    math_content: str | None = None,
) -> dict:
    return {
        "block_uid": uid,
        "block_type": block_type,
        "page_idx": page_idx,
        "block_seq": seq,
        "plain_text": text,
        # AnGIneer bbox 为 0~1 归一化（此处由像素坐标 ÷ 页面尺寸 1190×1684 换算）
        "bbox": [50 / 1190, (100 + y) / 1684, 1140 / 1190, (140 + y) / 1684],
        "derived_level": derived_level,
        "parent_uid": None,
        "source": source,
        "confidence": confidence,
        "image_path": image_path,
        "table_html": table_html,
        "math_content": math_content,
        "formula_body": None,
        "formula_number": None,
    }


def price_table_html(total: str) -> str:
    return (
        "<table>"
        "<tr><td>项目</td><td>金额</td></tr>"
        "<tr><td>分部分项工程费</td><td>800,000.00</td></tr>"
        "<tr><td>措施费</td><td>100,000.00</td></tr>"
        f"<tr><td>投标总价（元）</td><td>{total}</td></tr>"
        "</table>"
    )


def make_raw_doc(
    doc_id: str,
    *,
    file_name: str,
    author: str | None,
    created_at: str | None,
    blocks: list[dict],
    role: str = "bid",
) -> dict:
    return {
        "docId": doc_id,
        "role": role,
        "blocks": blocks,
        "meta": {
            "docMeta": {
                "fileName": file_name,
                "pageCount": 1,
                "author": author,
                "creatorTool": "Microsoft Word",
                "createdAt": created_at,
                "modifiedAt": None,
            },
            "outlines": [],
            "pages": [dict(PAGE)],
        },
    }


def adapt(raw: dict) -> IrDocument:
    """raw dict → 校验 → 适配 → 内部模型（测试统一入口，与生产路径一致）。"""
    return adapt_document(validate_raw_document(raw))


def load_raw_fixture(name: str) -> dict:
    return json.loads((FIXTURES / f"{name}.json").read_text(encoding="utf-8"))


@pytest.fixture()
def raw_doc_a() -> dict:
    return make_raw_doc(
        "doc-a",
        file_name="A公司投标文件.pdf",
        author="张三",
        created_at="2026-07-01T10:00:00",
        blocks=[
            make_raw_block("b001", "投标函", block_type="title", derived_level=1, y=0),
            make_raw_block("b002", A_INTRO, seq=2, y=60),
            make_raw_block("b003", SHARED_PARAGRAPH, seq=3, y=120),
            make_raw_block("b004", A_SEAL_LINE, seq=4, y=180),
            make_raw_block("b005", "", block_type="table", seq=5, y=240,
                           table_html=price_table_html("1,000,000.00"),
                           image_path="images/price.jpg"),
        ],
    )


@pytest.fixture()
def raw_doc_b() -> dict:
    return make_raw_doc(
        "doc-b",
        file_name="B公司投标文件.pdf",
        author="张三",
        created_at="2026-07-01T10:00:00",
        blocks=[
            make_raw_block("b001", "投标函", block_type="title", derived_level=1, y=0),
            make_raw_block("b002", B_INTRO, seq=2, y=60),
            make_raw_block("b003", SHARED_PARAGRAPH, seq=3, y=120),
            make_raw_block("b004", "", block_type="table", seq=4, y=180,
                           table_html=price_table_html("1,010,000.00"),
                           image_path="images/price.jpg"),
            # 低置信 OCR 块，文本与 doc-a 的 b004 完全相同 → 雷同但须降权（spec §4.5）
            make_raw_block("b005", A_SEAL_LINE, source="ocr", confidence=0.3, seq=5, y=240),
        ],
    )


@pytest.fixture()
def raw_doc_c() -> dict:
    return make_raw_doc(
        "doc-c",
        file_name="C公司投标文件.pdf",
        author="李四",
        created_at="2026-07-02T09:30:00",
        blocks=[
            make_raw_block("b001", C_TITLE, block_type="title", derived_level=1, y=0),
            make_raw_block("b002", C_PARA_1, seq=2, y=60),
            make_raw_block("b003", C_PARA_2, seq=3, y=120),
            make_raw_block("b004", "", block_type="table", seq=4, y=180,
                           table_html=price_table_html("1,020,000.00"),
                           image_path="images/price.jpg"),
        ],
    )


@pytest.fixture()
def ir_doc_a(raw_doc_a) -> IrDocument:
    return adapt(raw_doc_a)


@pytest.fixture()
def ir_doc_b(raw_doc_b) -> IrDocument:
    return adapt(raw_doc_b)


@pytest.fixture()
def ir_doc_c(raw_doc_c) -> IrDocument:
    return adapt(raw_doc_c)


@pytest.fixture()
def ir_docs(ir_doc_a, ir_doc_b, ir_doc_c) -> list[IrDocument]:
    return [ir_doc_a, ir_doc_b, ir_doc_c]


@pytest.fixture()
def ir_payload(raw_doc_a, raw_doc_b, raw_doc_c) -> dict:
    """可直接 POST 的请求体（raw 产物形态，Task 16 接口测试用）。"""
    return {"taskId": "task-001", "documents": [raw_doc_a, raw_doc_b, raw_doc_c]}


@pytest.fixture()
def raw_haigang_pair() -> list[dict]:
    return [load_raw_fixture("haigang1"), load_raw_fixture("haigang2")]


@pytest.fixture()
def raw_pingshen_pair() -> list[dict]:
    return [load_raw_fixture("pingshen_a"), load_raw_fixture("pingshen_b")]
