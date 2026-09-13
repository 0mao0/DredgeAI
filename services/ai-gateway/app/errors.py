"""库异常 -> HTTP 状态码/错误码映射；SSE 端点复用同一映射产出 error 事件。

客户端请求体校验由 main.py 的 RequestValidationError 处理器返回 400；这里**不做**
`ValueError -> 400` 兜底：库自身不抛 ValueError（`LLMError` 继承 `Exception`），能走到兜底的
ValueError 只可能来自服务端配置（`LLM_CONFIGS` 非法 JSON 的 `JSONDecodeError`、字段非法的
pydantic `ValidationError`）或网关内部缺陷，报成 400 会把排障方向引向调用方。
"""
from ai_inference.errors import (
    AllProvidersFailedError,
    LLMStreamError,
    LLMTruncatedError,
    ProviderAuthError,
    ProviderUnavailableError,
    RateLimitedError,
)


def error_status(exc: Exception) -> tuple[int, str]:
    if isinstance(exc, ProviderAuthError):
        return 401, "PROVIDER_AUTH"
    if isinstance(exc, RateLimitedError):
        return 429, "RATE_LIMITED"
    if isinstance(exc, LLMTruncatedError):
        return 502, "LLM_TRUNCATED"
    if isinstance(exc, (ProviderUnavailableError, AllProvidersFailedError, LLMStreamError)):
        return 502, "PROVIDER_UNAVAILABLE"
    # 未映射异常一律 500：宁可让调用方按服务端错误告警/重试，也不谎报为「请求非法」
    return 500, "INTERNAL_ERROR"


class LlmHttpError(Exception):
    def __init__(self, status_code: int, code: str, message: str, details: dict | None = None):
        super().__init__(message)
        self.status_code = status_code
        self.code = code
        self.message = message
        self.details = details
