import pytest
from fastapi import HTTPException
from starlette.requests import Request

from server import DEFAULT_VOICE_ID, require_key, _resolve_voice_id


def _req(headers=None):
    h = [(k.lower().encode(), v.encode()) for k, v in (headers or {}).items()]
    return Request({
        "type": "http", "method": "GET", "path": "/", "query_string": b"",
        "headers": h, "server": ("test", 80), "client": ("test", 80), "scheme": "http",
    })


@pytest.mark.asyncio
async def test_require_key_rejects_missing():
    with pytest.raises(HTTPException) as exc:
        await require_key(_req())
    assert exc.value.status_code == 401


@pytest.mark.asyncio
async def test_require_key_accepts_matching():
    await require_key(_req({"X-Meeting-Bot-Key": "dev-key"}))


def test_resolve_voice_falls_back_to_default():
    assert _resolve_voice_id({"zh-male-news": "x"}, "not-exist") == DEFAULT_VOICE_ID
    assert _resolve_voice_id({"zh-male-news": "x"}, "zh-male-news") == "zh-male-news"
