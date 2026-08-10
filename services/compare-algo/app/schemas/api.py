"""请求/响应与错误模型。请求为 AnGIneer 产物形态（v2 修订）；文档份数约束 2~5（spec §1）。"""
from pydantic import BaseModel, Field, model_validator

from app.angineer.raw import RawDocumentEnvelope
from app.schemas.evidence import Evidence


class AnalyzeRequest(BaseModel):
    taskId: str = Field(min_length=1)
    documents: list[RawDocumentEnvelope] = Field(min_length=2, max_length=5)

    @model_validator(mode="after")
    def _check_unique_doc_ids(self) -> "AnalyzeRequest":
        ids = [d.docId for d in self.documents]
        dups = sorted({i for i in ids if ids.count(i) > 1})
        if dups:
            raise ValueError(f"documents 中 docId 重复：{dups}")
        return self


class AnalyzeResponse(BaseModel):
    evidences: list[Evidence]


class ErrorDetail(BaseModel):
    path: str
    message: str


class ErrorResponse(BaseModel):
    code: str
    message: str
    details: list[ErrorDetail] = Field(default_factory=list)
