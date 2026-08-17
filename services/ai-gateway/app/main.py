"""ai-gateway FastAPI 入口：healthz / models / chat / chat/stream + 统一错误处理。"""
import logging
import threading

from ai_inference import LLMClient, achat_result_guarded, load_llm_config_from_env
from ai_inference.errors import LLMError
from fastapi import Depends, FastAPI, Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

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

app = FastAPI(title="ai-gateway", version="0.1.0")


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
            details={"errors": exc.errors()},
        ).model_dump(),
    )


@app.exception_handler(ValueError)
async def value_error_handler(request: Request, exc: ValueError) -> JSONResponse:
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
        finish_reason=result.finish_reason,
        usage=result.usage,
        used_config=result.used_config,
        used_model=result.used_model,
        attempts=result.attempts or 1,
        latency_seconds=result.latency_seconds,
        circuit_breaker_state=result.circuit_breaker_state,
    )
