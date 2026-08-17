from typing import Any

from pydantic import BaseModel, ConfigDict, Field, field_validator
from pydantic.alias_generators import to_camel


class BaseApiModel(BaseModel):
    """对外契约统一 camelCase（请求/响应）；同时兼容 snake_case 输入。"""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


class ChatMessage(BaseApiModel):
    role: str
    content: Any = None


class ChatRequest(BaseApiModel):
    messages: list[ChatMessage] = Field(min_length=1)
    mode: str | None = None
    config_name: str | None = None
    temperature: float | None = Field(default=None, ge=0.0, le=2.0)
    max_tokens: int | None = Field(default=None, ge=1)
    business: str = "general"

    @field_validator("mode")
    @classmethod
    def _check_mode(cls, v: str | None) -> str | None:
        if v is not None and v not in ("instruct", "thinking"):
            raise ValueError("mode 仅支持 instruct / thinking")
        return v


class ChatResponse(BaseApiModel):
    text: str
    finish_reason: str | None = None
    usage: dict[str, Any] | None = None
    used_config: str | None = None
    used_model: str | None = None
    attempts: int = 1
    latency_seconds: float | None = None
    circuit_breaker_state: str | None = None


class ErrorResponse(BaseModel):
    code: str
    message: str
    details: dict[str, Any] | None = None
