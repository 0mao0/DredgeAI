"""库异常 -> HTTP 状态码/错误码映射；SSE 端点复用同一映射产出 error 事件。"""
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
    if isinstance(exc, ValueError):
        return 400, "INVALID_REQUEST"
    return 500, "INTERNAL_ERROR"


class LlmHttpError(Exception):
    def __init__(self, status_code: int, code: str, message: str, details: dict | None = None):
        super().__init__(message)
        self.status_code = status_code
        self.code = code
        self.message = message
        self.details = details
