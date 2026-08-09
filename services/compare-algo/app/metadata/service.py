"""元数据比对：author / createdAt 一致性 + creatorTool 弱线索。metadata 类证据。"""
from app.display import display_names
from app.schemas.evidence import Evidence, EvidenceLocation, Severity, build_evidence
from app.schemas.ir import IrDocument


def _group_by_meta(documents: list[IrDocument], attr: str) -> dict[str, list[IrDocument]]:
    """按 meta 字段值分组；None/空串不参与（v2 §5-7：提取不到给 null）。

    相等性为归一后的字符串相等：createdAt 由适配层统一归一 ISO（真实 AnGIneer
    数据全为 PDF 原始日期，归一路径一致）；混合格式的同一时刻字符串
    （如 "...Z" 透传 vs "...+00:00" 归一）不会归并为一组——真实数据下可接受。
    """
    groups: dict[str, list[IrDocument]] = {}
    for d in documents:
        v = getattr(d.meta, attr)
        if v:
            groups.setdefault(v, []).append(d)
    return groups


def _meta_evidence(
    task_id: str,
    field: str,
    value: str,
    docs: list[IrDocument],
    severity: Severity,
    title: str,
    description: str,
    names: dict[str, str],
) -> Evidence:
    ordered = sorted(docs, key=lambda d: d.docId)
    return build_evidence(
        task_id=task_id,
        type="metadata",
        severity=severity,
        doc_ids=[d.docId for d in ordered],
        locations=[EvidenceLocation(docId=d.docId) for d in ordered],
        metrics={"field": field, "value": value},
        title=title,
        description=f"{description}涉及文件：{'、'.join(names[d.docId] for d in ordered)}。",
    )


def compare_meta_fields(task_id: str, documents: list[IrDocument]) -> list[Evidence]:
    names = display_names(documents)
    evidences: list[Evidence] = []
    for author, docs in sorted(_group_by_meta(documents, "author").items()):
        if len(docs) >= 2:
            evidences.append(_meta_evidence(
                task_id, "author", author, docs, "mid",
                f"{len(docs)} 份标书文件作者相同（{author}）",
                "文件元数据作者一致，疑似同一台设备/同一人编制。",
                names,
            ))
    for created, docs in sorted(_group_by_meta(documents, "createdAt").items()):
        if len(docs) >= 2:
            evidences.append(_meta_evidence(
                task_id, "createdAt", created, docs, "mid",
                f"{len(docs)} 份标书创建时间完全相同（{created}）",
                "文件创建时间完全一致，疑似同一批次生成。",
                names,
            ))
    for tool, docs in sorted(_group_by_meta(documents, "creatorTool").items()):
        if len(docs) == len(documents) and len(docs) >= 2:
            evidences.append(_meta_evidence(
                task_id, "creatorTool", tool, docs, "low",
                f"全部标书使用同一编制工具（{tool}）",
                "编制工具一致仅为弱线索，需结合其他证据判断。",
                names,
            ))
    return evidences


# ---------- 相同错别字检测：低频错字 n-gram 碰撞 ----------

from app.ocr import is_low_confidence_ocr
from app.similarity.shingle import SHINGLABLE_TYPES, normalize_text

TYPO_NGRAM = 6
_TYPO_SAMPLES_MAX = 10


def _block_typo_ngrams(doc: IrDocument, n: int = TYPO_NGRAM) -> dict[str, set[str]]:
    """blockId -> 可疑异常 n-gram 集合。

    可疑 = 该 gram 在全文仅出现一次，且包含「全文仅出现一次的字符」
    （生僻字/错别字特征；常规用字会多次出现，被自然过滤）。
    低置信 OCR 块不参与（spec §4.5：OCR「错得一样」可能只是识别器犯了同样的错）。
    """
    eligible = [
        b for b in doc.blocks
        if b.type in SHINGLABLE_TYPES and not is_low_confidence_ocr(b)
    ]
    full_text = "".join(normalize_text(b.text) for b in eligible)
    if len(full_text) < n:
        return {}
    gram_freq: dict[str, int] = {}
    for i in range(len(full_text) - n + 1):
        g = full_text[i : i + n]
        gram_freq[g] = gram_freq.get(g, 0) + 1
    char_freq: dict[str, int] = {}
    for ch in full_text:
        char_freq[ch] = char_freq.get(ch, 0) + 1
    result: dict[str, set[str]] = {}
    for b in eligible:
        text = normalize_text(b.text)
        grams = {
            text[i : i + n]
            for i in range(max(0, len(text) - n + 1))
            if gram_freq.get(text[i : i + n], 0) == 1
            and any(char_freq[c] == 1 for c in text[i : i + n])
        }
        if grams:
            result[b.blockId] = grams
    return result


def detect_shared_typos(
    task_id: str, documents: list[IrDocument], n: int = TYPO_NGRAM
) -> list[Evidence]:
    """相同错别字：可疑 n-gram 在 ≥2 份文档中逐字碰撞 → high 证据。

    同一文档组合的多条碰撞归并为一条证据，locations 定位到含碰撞串的块。
    """
    gram_index: dict[str, dict[str, str]] = {}  # gram -> {docId: blockId}
    for d in documents:
        for block_id, grams in _block_typo_ngrams(d, n).items():
            for g in grams:
                gram_index.setdefault(g, {})[d.docId] = block_id

    by_docs: dict[tuple[str, ...], list[tuple[str, dict[str, str]]]] = {}
    for g, m in gram_index.items():
        if len(m) >= 2:
            by_docs.setdefault(tuple(sorted(m)), []).append((g, m))

    evidences: list[Evidence] = []
    for doc_ids, hits in sorted(by_docs.items()):
        locations = [
            EvidenceLocation(
                docId=doc_id,
                blockIds=sorted({m[doc_id] for _, m in hits}),
            )
            for doc_id in doc_ids
        ]
        samples = sorted({g for g, _ in hits})[:_TYPO_SAMPLES_MAX]
        evidences.append(build_evidence(
            task_id=task_id,
            type="metadata",
            severity="high",
            doc_ids=list(doc_ids),
            locations=locations,
            metrics={
                "pattern": "shared-typo",
                "sharedNgramCount": len(hits),
                "samples": samples,
            },
            title=f"{len(doc_ids)} 份标书出现相同错别字/低频异常串（{len(hits)} 处）",
            description="多份标书出现逐字相同的低频异常字串；原生文本中「错得一样」是围标强证据。",
        ))
    return evidences


def analyze_metadata(task_id: str, documents: list[IrDocument]) -> list[Evidence]:
    return compare_meta_fields(task_id, documents) + detect_shared_typos(task_id, documents)
