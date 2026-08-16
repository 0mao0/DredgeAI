"""Evidence 模型：字段名逐字遵守 spec §6.1。

本服务只通过 build_evidence 产出 aiGenerated=false 的
similarity / pricing / metadata 三类证据；clause / indicator 属 compare-ai。
"""
from __future__ import annotations

from typing import Any, Literal
from uuid import uuid4

from pydantic import BaseModel, Field

EvidenceType = Literal["similarity", "pricing", "metadata", "clause", "indicator"]
Severity = Literal["high", "mid", "low"]


class EvidenceLocation(BaseModel):
    docId: str
    # 元数据类证据无块级定位，允许空列表（spec §6.1 未约束非空）
    blockIds: list[str] = Field(default_factory=list)


class Evidence(BaseModel):
    id: str
    taskId: str
    type: EvidenceType
    severity: Severity
    docIds: list[str] = Field(min_length=1)
    locations: list[EvidenceLocation] = Field(min_length=1)
    metrics: dict[str, Any] = Field(default_factory=dict)
    title: str
    description: str
    aiGenerated: bool


def build_evidence(
    *,
    task_id: str,
    type: EvidenceType,
    severity: Severity,
    doc_ids: list[str],
    locations: list[EvidenceLocation],
    metrics: dict[str, Any] | None = None,
    title: str,
    description: str,
) -> Evidence:
    """组装确定性证据：id 自动生成，aiGenerated 恒为 False（spec §3.2）。

    id 为 uuid4 随机值，仅供调试/日志关联：本服务是确定性计算服务，
    同一输入多次调用产出的 id 不同；调用方（C# 端）忽略该字段，
    不应持久化或以其做幂等键。
    """
    return Evidence(
        id=str(uuid4()),
        taskId=task_id,
        type=type,
        severity=severity,
        docIds=doc_ids,
        locations=locations,
        metrics=metrics or {},
        title=title,
        description=description,
        aiGenerated=False,
    )
