"""元数据比对：author / createdAt 一致性 + creatorTool 弱线索。metadata 类证据。"""
from app.display import display_names
from app.schemas.evidence import Evidence, EvidenceLocation, Severity, build_evidence
from app.schemas.ir import IrDocument
from app.settings import get_settings
from app.similarity.shingle import SHINGLABLE_TYPES, normalize_text


def _group_by_meta(documents: list[IrDocument], attr: str) -> dict[str, list[IrDocument]]:
    """按 meta 字段值分组；None/空串不参与（v2 §5-7：提取不到给 null）。

    值先 strip 再分组：「张三 」与「张三」归并为一组，展示/metrics 用 strip 后的值；
    纯空白串视同缺失。相等性为归一后的字符串相等：createdAt 由适配层统一归一
    ISO（真实 AnGIneer 数据全为 PDF 原始日期，归一路径一致）；混合格式的同一时刻
    字符串（如 "...Z" 透传 vs "...+00:00" 归一）不会归并为一组——真实数据下可接受。
    """
    groups: dict[str, list[IrDocument]] = {}
    for d in documents:
        v = getattr(d.meta, attr)
        if v and v.strip():
            groups.setdefault(v.strip(), []).append(d)
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

TYPO_NGRAM = 6        # 默认值文档；运行期以 settings.typo_ngram 为准
_TYPO_SAMPLES_MAX = 10  # 默认值文档；运行期以 settings.typo_samples_max 为准


def _native_text_blocks(doc: IrDocument) -> list:
    """原生文本块（source == "text"）中可参与 shingling 的块。"""
    return [
        b for b in doc.blocks
        if b.type in SHINGLABLE_TYPES and b.source == "text"
    ]


def _native_full_text(doc: IrDocument) -> str:
    """原生文本块（source == "text"）规范化后拼接的全文。"""
    return "".join(normalize_text(b.text) for b in _native_text_blocks(doc))


def _block_typo_ngrams(
    doc: IrDocument,
    n: int | None = None,
    full_text: str | None = None,
) -> dict[str, dict[str, int]]:
    """blockId -> {可疑异常 n-gram: 块内起始偏移}。

    可疑 = 该 gram 在全文仅出现一次，且包含「全文仅出现一次的字符」
    （生僻字/错别字特征；常规用字会多次出现，被自然过滤）。
    仅统计原生文本块（source == "text"）：OCR/表格/公式块中「错得一样」
    可能只是同一识别器对同一模板犯了同样的错（实测事实 #1：真实数据
    confidence 全 1.0，置信度过滤识别不出 OCR 误碰撞），非强证据。
    full_text 可由调用方预先算好传入（detect_shared_typos 中复用，避免重复拼接）。
    """
    if n is None:
        n = get_settings().typo_ngram
    eligible = _native_text_blocks(doc)
    if full_text is None:
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
    result: dict[str, dict[str, int]] = {}
    for b in eligible:
        text = normalize_text(b.text)
        grams = {
            text[i : i + n]: i
            for i in range(max(0, len(text) - n + 1))
            if gram_freq.get(text[i : i + n], 0) == 1
            and any(char_freq[c] == 1 for c in text[i : i + n])
        }
        if grams:
            result[b.blockId] = grams
    return result


def detect_shared_typos(
    task_id: str, documents: list[IrDocument], n: int | None = None
) -> list[Evidence]:
    """相同错别字：可疑 n-gram 在 ≥2 份文档中逐字碰撞 → metadata 证据。

    同一文档组合的多条碰撞归并为一条证据，locations 定位到含碰撞串的块。
    去重站点计数：以组合内 docId 排序首文档为参照，同块内窗口起点落在当前
    连续段覆盖区间内的碰撞合并为极大连续段，每段计 1 处——一个错字会被
    ~n 个滑动窗口同时覆盖，窗口数不等于错字数。≥2 处 → high（「错得一样」
    强证据）；仅 1 处 → mid（套话/正式用语巧合可能，文案降为疑似）。
    samples/items 每处取中间窗口为代表，统一按 typo_samples_max 截断防膨胀；
    样本为规范化文本（标点/空白已剥离），前端高亮需先做同样规范化再定位。
    """
    if n is None:
        n = get_settings().typo_ngram
    samples_max = get_settings().typo_samples_max
    names = display_names(documents)
    full_texts = {d.docId: _native_full_text(d) for d in documents}
    gram_index: dict[str, dict[str, tuple[str, int]]] = {}  # gram -> {docId: (blockId, start)}
    for d in documents:
        for block_id, grams in _block_typo_ngrams(d, n, full_text=full_texts[d.docId]).items():
            for g, start in grams.items():
                gram_index.setdefault(g, {})[d.docId] = (block_id, start)

    by_docs: dict[tuple[str, ...], list[tuple[str, dict[str, tuple[str, int]]]]] = {}
    for g, m in gram_index.items():
        if len(m) >= 2:
            by_docs.setdefault(tuple(sorted(m)), []).append((g, m))

    evidences: list[Evidence] = []
    for doc_ids, hits in sorted(by_docs.items()):
        # 全文完全一致的副本组合不出 typo 证据：雷同已由 similarity 证据
        # （Dice=1.0）覆盖，「错得一样」不再提供额外区分度（实测事实 #7：
        # 评审办法副本对仅 author + creatorTool 两条元数据证据）
        if len({full_texts[i] for i in doc_ids}) == 1:
            continue
        ref = doc_ids[0]
        spans = sorted((m[ref][0], m[ref][1], g) for g, m in hits)
        runs: list[list[str]] = []  # 极大连续段（段内为按起点升序的 gram）
        run_block = ""
        run_end = -1
        for block_id, start, g in spans:
            if block_id == run_block and start < run_end:
                runs[-1].append(g)
                run_end = max(run_end, start + n)
            else:
                runs.append([g])
                run_block = block_id
                run_end = start + n
        site_count = len(runs)
        locations = [
            EvidenceLocation(
                docId=doc_id,
                blockIds=sorted({m[doc_id][0] for _, m in hits}),
            )
            for doc_id in doc_ids
        ]
        hit_map = {g: m for g, m in hits}
        items = []
        for i, run in enumerate(runs[:samples_max], start=1):
            rep = run[len(run) // 2]
            rep_map = hit_map[rep]
            items.append({
                "index": i,
                "text": rep,
                "blockIds": {doc_id: rep_map[doc_id][0] for doc_id in doc_ids},
                "starts": {doc_id: rep_map[doc_id][1] for doc_id in doc_ids},
            })
        samples = [item["text"] for item in items]
        if site_count >= 2:
            severity: Severity = "high"
            title = f"{len(doc_ids)} 份标书出现相同错别字/低频异常串（{site_count} 处）"
            description = "多份标书出现逐字相同的低频异常字串；原生文本中「错得一样」是围标强证据。"
        else:
            severity = "mid"
            title = f"{len(doc_ids)} 份标书疑似相同错别字/低频用字（{site_count} 处）"
            description = "多份标书出现逐字相同的低频异常字串；仅单处命中，可能为行业套话或正式用语巧合，建议人工复核。"
        evidences.append(build_evidence(
            task_id=task_id,
            type="metadata",
            severity=severity,
            doc_ids=list(doc_ids),
            locations=locations,
            metrics={
                "pattern": "shared-typo",
                "sharedNgramCount": site_count,
                "samples": samples,
                "items": items,
            },
            title=title,
            description=f"{description}涉及文件：{'、'.join(names[doc_id] for doc_id in doc_ids)}。",
        ))
    return evidences


def analyze_metadata(task_id: str, documents: list[IrDocument]) -> list[Evidence]:
    return compare_meta_fields(task_id, documents) + detect_shared_typos(task_id, documents)
