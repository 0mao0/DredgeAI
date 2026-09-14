"""ai-gateway FastAPI 入口：healthz / models / chat / chat/stream + 统一错误处理。"""
import json
import logging
import threading
from contextlib import asynccontextmanager

from ai_inference import LLMClient, achat_result_guarded, load_llm_config_from_env
from ai_inference.errors import LLMError
from fastapi import Depends, FastAPI, Request
from fastapi.encoders import jsonable_encoder
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse, StreamingResponse

from app.errors import LlmHttpError, error_status
from app.schemas import ChatRequest, ChatResponse, ErrorResponse
from app.settings import get_settings
from app.usage import enqueue_usage, usage_payload

logger = logging.getLogger("ai-gateway")


def _configure_logging() -> None:
    root = logging.getLogger()
    if root.handlers:
        return
    handler = logging.StreamHandler()
    handler.setFormatter(logging.Formatter("%(asctime)s %(levelname)s [%(name)s] %(message)s"))
    root.addHandler(handler)
    root.setLevel(logging.INFO)


_configure_logging()


@asynccontextmanager
async def lifespan(_app: FastAPI):
    """启动期配置自检：把配置错误从「首次请求才暴露」提前到启动可见。

    只告警不拦启动——进程起得来、/healthz 可探活，配置问题由运维在日志里定位；
    请求路径上的配置异常仍按 500 上报（不再冒充客户端 400）。
    """
    try:
        logger.info("LLM 配置自检通过：%d 个模型", len(llm_client().configs))
    except Exception as exc:
        logger.warning("LLM 配置自检失败（服务仍启动，请检查 LLM_CONFIGS / ANGINEER_*）：%s", exc)
    yield


app = FastAPI(title="ai-gateway", version="0.1.0", lifespan=lifespan)


def require_api_token(request: Request) -> None:
    token = get_settings().api_token
    if token and request.headers.get("X-API-Key") != token:
        raise LlmHttpError(401, "UNAUTHORIZED", "无效的网关令牌")


_client: LLMClient | None = None
_client_lock = threading.Lock()


def llm_client() -> LLMClient:
    """进程内单例：由 env（LLM_CONFIGS + ANGINEER_*）构造；测试通过 monkeypatch 本函数替换。"""
    global _client
    if _client is None:
        with _client_lock:
            if _client is None:
                _client = LLMClient(load_llm_config_from_env())
    return _client


@app.exception_handler(LlmHttpError)
async def llm_http_error_handler(request: Request, exc: LlmHttpError) -> JSONResponse:
    return JSONResponse(
        status_code=exc.status_code,
        content=ErrorResponse(code=exc.code, message=exc.message, details=exc.details).model_dump(),
    )


@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request: Request, exc: RequestValidationError) -> JSONResponse:
    return JSONResponse(
        status_code=400,
        content=ErrorResponse(
            code="INVALID_REQUEST",
            message="请求体校验失败",
            # 必须经 jsonable_encoder：字段校验器抛 ValueError 时 pydantic 会把该异常对象
            # 放进 errors() 的 ctx.error，直接塞进 JSONResponse 会 TypeError（400 变 500）
            details={"errors": jsonable_encoder(exc.errors())},
        ).model_dump(),
    )


@app.exception_handler(ValueError)
async def value_error_handler(request: Request, exc: ValueError) -> JSONResponse:
    # 客户端请求体校验由 RequestValidationError 处理器负责；能走到这里的 ValueError
    # 属服务端配置/内部错误（LLM_CONFIGS 非法 JSON、pydantic ValidationError 等），
    # 不能冒充客户端错误：调用方对 4xx 不重试，会把服务端配置问题当自身请求非法排查
    logger.warning("服务端 ValueError on %s: %s", request.url.path, exc)
    status, code = error_status(exc)
    return JSONResponse(
        status_code=status,
        content=ErrorResponse(code=code, message=str(exc)).model_dump(),
    )


@app.exception_handler(Exception)
async def unhandled_exception_handler(request: Request, exc: Exception) -> JSONResponse:
    logger.exception("unhandled error on %s: %s", request.url.path, exc)
    return JSONResponse(
        status_code=500,
        content=ErrorResponse(code="INTERNAL_ERROR", message="内部错误").model_dump(),
    )


@app.get("/healthz")
def healthz() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/v1/models", dependencies=[Depends(require_api_token)])
def get_models() -> dict:
    return {"models": llm_client().configs}


def _require_models() -> None:
    if not llm_client().configs:
        raise LlmHttpError(503, "NO_MODELS_CONFIGURED", "LLM_CONFIGS 为空或未启用任何模型")


@app.post("/v1/chat", response_model=ChatResponse, dependencies=[Depends(require_api_token)])
async def post_chat(req: ChatRequest) -> ChatResponse:
    _require_models()
    messages = [m.model_dump() for m in req.messages]
    try:
        result = await achat_result_guarded(
            llm_client(),
            messages,
            mode=req.mode or "instruct",
            config_name=req.config_name,
            temperature=req.temperature,
            max_tokens=req.max_tokens,
        )
    except LLMError as exc:
        status, code = error_status(exc)
        raise LlmHttpError(status, code, str(exc)) from exc

    enqueue_usage(usage_payload(
        business=req.business,
        text=result.text or "",
        finish_reason=result.finish_reason,
        usage=result.usage,
        used_config=result.used_config,
        used_model=result.used_model,
        attempts=result.attempts or 1,
        latency_seconds=result.latency_seconds,
        circuit_breaker_state=result.circuit_breaker_state,
        success=True,
    ))
    return ChatResponse(
        text=result.text or "",
        reasoning=result.reasoning,
        finish_reason=result.finish_reason,
        usage=result.usage,
        used_config=result.used_config,
        used_model=result.used_model,
        attempts=result.attempts or 1,
        latency_seconds=result.latency_seconds,
        circuit_breaker_state=result.circuit_breaker_state,
    )


_EVENT_KEY_MAP = {
    "finish_reason": "finishReason",
    "used_config": "usedConfig",
    "used_model": "usedModel",
    "latency_seconds": "latencySeconds",
    "circuit_breaker_state": "circuitBreakerState",
    "error_type": "errorType",
}


def _to_contract_event(event: dict) -> dict:
    """把库原生 snake_case 事件字段转换为对外契约 camelCase（type/text/usage/attempts 不变）。"""
    return {_EVENT_KEY_MAP.get(key, key): value for key, value in event.items()}


def _sse(event: dict) -> str:
    return f"data: {json.dumps(_to_contract_event(event), ensure_ascii=False)}\n\n"


@app.post("/v1/chat/stream", dependencies=[Depends(require_api_token)])
async def post_chat_stream(req: ChatRequest) -> StreamingResponse:
    _require_models()
    messages = [m.model_dump() for m in req.messages]

    async def generate():
        try:
            async for event in llm_client().achat_stream_events(
                messages,
                mode=req.mode or "instruct",
                config_name=req.config_name,
                temperature=req.temperature,
                max_tokens=req.max_tokens,
            ):
                if event["type"] == "done":
                    enqueue_usage(usage_payload(
                        business=req.business,
                        text="",
                        finish_reason=event.get("finish_reason"),
                        usage=event.get("usage"),
                        used_config=event.get("used_config"),
                        used_model=event.get("used_model"),
                        attempts=event.get("attempts") or 1,
                        latency_seconds=event.get("latency_seconds"),
                        circuit_breaker_state=event.get("circuit_breaker_state"),
                        success=True,
                    ))
                yield _sse(event)
        except LLMError as exc:
            status, code = error_status(exc)
            yield _sse({"type": "error", "error": {"type": code, "message": str(exc)}})

    return StreamingResponse(
        generate(),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )
