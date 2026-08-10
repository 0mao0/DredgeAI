"""compare-algo FastAPI 入口：三个分析接口 + 统一错误处理。

无状态计算服务，由 ABP 主服务调用；请求体为 AnGIneer 解析产物原文
（doc_blocks_graph.jsonl 节点 + meta 的 {docMeta, outlines, pages}），
本服务不直接对接 AnGIneer/MinerU。产物经 app/angineer/ 适配层转为内部模型后分析。
"""
import logging
import os

from fastapi import FastAPI, Request
from fastapi.exceptions import RequestValidationError, ResponseValidationError
from fastapi.responses import JSONResponse
from pydantic import ValidationError

from app.angineer.adapter import adapt_document
from app.metadata.service import analyze_metadata
from app.pricing.service import analyze_pricing
from app.schemas.api import (
    AnalyzeRequest,
    AnalyzeResponse,
    ErrorDetail,
    ErrorResponse,
)
from app.similarity.service import analyze_similarity

logger = logging.getLogger("compare-algo")


class BodySizeLimitMiddleware:
    """请求体大小限制（parse-DoS 防御）：超过限额直接 413，不进入 JSON 解析。

    纯 ASGI 实现：声明的 Content-Length 超限走快路径直接拒绝；否则完整接收
    body（含 chunked/无 Content-Length 的情况）并计数，未超限时原样回放给下游；
    接收途中客户端断开（http.disconnect）直接返回，不回放截断的 body。
    """

    def __init__(self, app, max_body_bytes: int):
        self.app = app
        self.max_body_bytes = max_body_bytes

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http":
            await self.app(scope, receive, send)
            return
        # 快路径：声明的 Content-Length 已超限直接 413，不读 body；
        # 下方字节计数仍是真正的防线（chunked / 无 Content-Length / 谎报）
        declared = _header_value(scope, b"content-length")
        if declared is not None:
            try:
                if int(declared) > self.max_body_bytes:
                    await self._send_413(send)
                    return
            except ValueError:
                pass  # 非法 Content-Length 交由字节计数兜底
        chunks: list[bytes] = []
        size = 0
        while True:
            message = await receive()
            if message["type"] == "http.disconnect":
                return  # 客户端断开：不再向下游回放截断的 body
            if message["type"] != "http.request":
                break
            chunks.append(message.get("body", b""))
            size += len(chunks[-1])
            if size > self.max_body_bytes:
                await self._send_413(send)
                return
            if not message.get("more_body", False):
                break
        body = b"".join(chunks)
        replayed = False

        async def replay_receive():
            nonlocal replayed
            if replayed:
                return {"type": "http.request", "body": b"", "more_body": False}
            replayed = True
            return {"type": "http.request", "body": body, "more_body": False}

        await self.app(scope, replay_receive, send)

    async def _send_413(self, send):
        payload = ErrorResponse(
            code="REQUEST_TOO_LARGE",
            message=f"请求体超过大小限制（{self.max_body_bytes} 字节）",
        ).model_dump_json().encode()
        await send({
            "type": "http.response.start",
            "status": 413,
            "headers": [
                (b"content-type", b"application/json"),
                (b"content-length", str(len(payload)).encode()),
            ],
        })
        await send({"type": "http.response.body", "body": payload})


def _header_value(scope: dict, name: bytes) -> bytes | None:
    for key, value in scope.get("headers", []):
        if key.lower() == name:
            return value
    return None


_DEFAULT_MAX_BODY_BYTES = 50 * 1024 * 1024  # 默认 50MB（实测 5 份真实文档产物 ≈ 15MB）


def _max_body_bytes() -> int:
    raw = os.environ.get("COMPARE_ALGO_MAX_BODY_BYTES", "")
    try:
        return int(raw)
    except ValueError:
        if raw:  # 未设置属正常；设置了却解析不了才告警（"50MB"、"²" 等）
            logger.warning(
                "COMPARE_ALGO_MAX_BODY_BYTES=%r 无法解析为整数，回退默认 %d 字节",
                raw, _DEFAULT_MAX_BODY_BYTES,
            )
        return _DEFAULT_MAX_BODY_BYTES


app = FastAPI(title="compare-algo", version="0.1.0")
app.add_middleware(BodySizeLimitMiddleware, max_body_bytes=_max_body_bytes())


def _format_error_detail(e: dict) -> ErrorDetail:
    # FastAPI 给请求体错误加 "body" 前缀，剥掉后对调用方更直观
    loc = [str(p) for p in e.get("loc", ()) if p != "body"]
    # 文档级/请求级 model_validator（block_uid 重复、docId 重复）loc 为空或为
    # documents[i] 层级，无字段路径；空 loc 兜底渲染为 documents 根，避免空 path
    path = ".".join(loc) if loc else "documents"
    return ErrorDetail(path=path, message=e.get("msg", ""))


def _validation_error_body(errors) -> ErrorResponse:
    details = [_format_error_detail(e) for e in errors]
    return ErrorResponse(
        code="IR_VALIDATION_FAILED",
        message="产物校验失败，详见 details",
        details=details,
    )


@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request: Request, exc: RequestValidationError) -> JSONResponse:
    """请求体（产物）校验不合格 → 422 + 具体字段错误（tech 决策）。"""
    body = _validation_error_body(exc.errors())
    return JSONResponse(status_code=422, content=body.model_dump())


@app.exception_handler(ValidationError)
async def adapt_exception_handler(request: Request, exc: ValidationError) -> JSONResponse:
    """适配输出违反内部模型底线（如 outline 引用缺失）→ 422。"""
    body = _validation_error_body(exc.errors())
    return JSONResponse(status_code=422, content=body.model_dump())


@app.exception_handler(ResponseValidationError)
async def response_validation_exception_handler(
    request: Request, exc: ResponseValidationError
) -> JSONResponse:
    """服务端产出违反响应契约是服务端 bug → 500，不让调用方按 422 背锅。

    必须显式注册：旧版 FastAPI 的响应校验失败抛 pydantic ValidationError，
    会落入上方适配层 422 处理器；新版抛 ResponseValidationError 则由通用
    Exception 处理器兜底并向上重抛。此处统一按 500 处理并记录违规详情。
    """
    logger.error(
        "response validation failed on %s: %s", request.url.path, exc,
    )
    body = ErrorResponse(code="INTERNAL_ERROR", message="内部分析失败，请联系算法服务负责人")
    return JSONResponse(status_code=500, content=body.model_dump())


@app.exception_handler(Exception)
async def unhandled_exception_handler(request: Request, exc: Exception) -> JSONResponse:
    """未知异常 → 500，堆栈只进日志不进响应体。"""
    logger.exception("unhandled error on %s: %s", request.url.path, exc)
    body = ErrorResponse(code="INTERNAL_ERROR", message="内部分析失败，请联系算法服务负责人")
    return JSONResponse(status_code=500, content=body.model_dump())


@app.get("/healthz")
def healthz() -> dict[str, str]:
    return {"status": "ok"}


def _adapt(req: AnalyzeRequest):
    return [adapt_document(d) for d in req.documents]


@app.post("/analyze/similarity", response_model=AnalyzeResponse)
def post_analyze_similarity(req: AnalyzeRequest) -> AnalyzeResponse:
    """两两查重 + 雷同簇：同一文档集可同时产出 pairwise 与 cluster 证据，
    消费方按 type + docIds / metrics.cluster 分组。"""
    return AnalyzeResponse(evidences=analyze_similarity(req.taskId, _adapt(req)))


@app.post("/analyze/pricing", response_model=AnalyzeResponse)
def post_analyze_pricing(req: AnalyzeRequest) -> AnalyzeResponse:
    """报价规律分析：多个检测器（等差/贴近度/尾数）可在同一文档集上同时命中，
    消费方按 type + docIds / metrics.pattern 分组。"""
    return AnalyzeResponse(evidences=analyze_pricing(req.taskId, _adapt(req)))


@app.post("/analyze/metadata", response_model=AnalyzeResponse)
def post_analyze_metadata(req: AnalyzeRequest) -> AnalyzeResponse:
    return AnalyzeResponse(evidences=analyze_metadata(req.taskId, _adapt(req)))
