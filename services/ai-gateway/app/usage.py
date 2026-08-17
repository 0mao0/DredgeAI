"""用量上报：fire-and-forget POST 到 ABP ingest 端点；失败仅记 warning，不影响业务响应。"""
import asyncio
import logging
from datetime import datetime, timezone

import httpx

from app.settings import get_settings

logger = logging.getLogger("ai-gateway")

_tasks: set[asyncio.Task] = set()


def _spawn(coro) -> None:
    task = asyncio.create_task(coro)
    _tasks.add(task)
    task.add_done_callback(_tasks.discard)


def usage_payload(
    *,
    business: str,
    text: str,
    finish_reason: str | None,
    usage: dict | None,
    used_config: str | None,
    used_model: str | None,
    attempts: int,
    latency_seconds: float | None,
    circuit_breaker_state: str | None,
    success: bool,
    error_type: str | None = None,
    error_message: str | None = None,
) -> dict:
    return {
        "business": business,
        "usedConfig": used_config,
        "usedModel": used_model,
        "inputTokens": (usage or {}).get("prompt_tokens"),
        "outputTokens": (usage or {}).get("completion_tokens"),
        "totalTokens": (usage or {}).get("total_tokens"),
        "finishReason": finish_reason,
        "attempts": attempts,
        "latencySeconds": latency_seconds,
        "circuitBreakerState": circuit_breaker_state,
        "success": success,
        "errorType": error_type,
        "errorMessage": error_message,
        "requestedAt": datetime.now(timezone.utc).isoformat(),
        "textPreview": text[:200],
    }


def enqueue_usage(payload: dict) -> None:
    _spawn(report_usage(payload))


async def report_usage(payload: dict) -> None:
    settings = get_settings()
    if not settings.usage_report_enabled:
        return
    headers = {}
    if settings.ingest_token:
        headers["X-Gateway-Token"] = settings.ingest_token
    try:
        async with httpx.AsyncClient(timeout=5.0) as client:
            response = await client.post(settings.usage_report_url, json=payload, headers=headers)
            response.raise_for_status()
    except Exception as exc:  # noqa: BLE001 - 上报失败不影响主链路
        logger.warning("usage report failed: %s", exc)
