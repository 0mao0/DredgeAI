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
