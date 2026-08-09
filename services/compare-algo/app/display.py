"""面向用户的文档标识解析：证据 title/description 中的文档名统一走这里（通用约定）。"""
from app.schemas.ir import IrDocument


def display_names(documents: list[IrDocument]) -> dict[str, str]:
    """docId -> 展示名：优先 meta.fileName，缺失/空串时回退 docId。"""
    return {d.docId: (d.meta.fileName or d.docId) for d in documents}
