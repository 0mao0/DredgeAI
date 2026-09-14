import logging

import pytest
from ai_inference import (
    AllProvidersFailedError,
    ChatResult,
    ProviderAuthError,
    RateLimitedError,
)
from fastapi.testclient import TestClient

from app.main import app

client = TestClient(app)


def test_healthz():
    r = client.get("/healthz")
    assert r.status_code == 200
    assert r.json() == {"status": "ok"}


def test_models(fake_client):
    r = client.get("/v1/models")
    assert r.status_code == 200
    assert r.json()["models"] == fake_client.configs


def test_chat_success(fake_client):
    r = client.post("/v1/chat", json={
        "messages": [{"role": "user", "content": "你好"}],
        "mode": "thinking",
        "business": "standard-qa",
    })
    assert r.status_code == 200
    body = r.json()
    assert body["text"] == "ok"
    assert body["usedConfig"] == "fake"
    assert body["attempts"] == 1
    assert fake_client.calls[0]["mode"] == "thinking"


@pytest.mark.parametrize("error,status,code", [
    (ProviderAuthError("bad key"), 401, "PROVIDER_AUTH"),
    (RateLimitedError("429"), 429, "RATE_LIMITED"),
    (AllProvidersFailedError("all down"), 502, "PROVIDER_UNAVAILABLE"),
])
def test_chat_error_mapping(fake_client, error, status, code):
    fake_client.error = error
    r = client.post("/v1/chat", json={"messages": [{"role": "user", "content": "hi"}]})
    assert r.status_code == status
    assert r.json()["code"] == code


def test_chat_truncated_maps_502(fake_client):
    # 截断守卫重试一次仍截断 -> LLMTruncatedError -> 502 LLM_TRUNCATED
    fake_client.result = ChatResult(
        text="partial", finish_reason="length",
        attempts=1, latency_seconds=0.1, used_config="fake", used_model="fake-model",
    )
    r = client.post("/v1/chat", json={"messages": [{"role": "user", "content": "x" * 10}]})
    assert r.status_code == 502
    assert r.json()["code"] == "LLM_TRUNCATED"


def test_chat_invalid_body():
    r = client.post("/v1/chat", json={"messages": []})
    assert r.status_code == 400
    assert r.json()["code"] == "INVALID_REQUEST"


def test_chat_invalid_mode_still_400():
    # 请求体级 ValueError（mode 白名单）仍由 RequestValidationError 处理器报 400
    r = client.post("/v1/chat", json={"messages": [{"role": "user", "content": "hi"}], "mode": "bogus"})
    assert r.status_code == 400
    assert r.json()["code"] == "INVALID_REQUEST"


def test_chat_config_error_maps_500(monkeypatch):
    # 服务端配置类异常不得冒充客户端 400：LLM_CONFIGS 非法 JSON 的 JSONDecodeError 与
    # 字段非法的 pydantic ValidationError 都是 ValueError 子类，400 只代表请求体非法
    def _boom():
        raise ValueError("LLM_CONFIGS 不是合法 JSON")

    monkeypatch.setattr("app.main.llm_client", _boom)
    r = client.post("/v1/chat", json={"messages": [{"role": "user", "content": "hi"}]})
    assert r.status_code == 500
    assert r.json()["code"] == "INTERNAL_ERROR"


def test_startup_config_selfcheck_warns(monkeypatch, caplog):
    # lifespan 自检：配置加载失败只 WARNING，不拦启动（测试端需 with TestClient 才触发 lifespan）
    def _boom():
        raise ValueError("LLM_CONFIGS 不是合法 JSON")

    monkeypatch.setattr("app.main.llm_client", _boom)
    with caplog.at_level(logging.WARNING):
        with TestClient(app) as c:
            assert c.get("/healthz").status_code == 200
    assert any("LLM 配置自检失败" in record.getMessage() for record in caplog.records)


def test_chat_no_models_503(fake_client, monkeypatch):
    class EmptyClient:
        @property
        def configs(self) -> list[dict]:
            return []
    monkeypatch.setattr("app.main.llm_client", lambda: EmptyClient())
    r = client.post("/v1/chat", json={"messages": [{"role": "user", "content": "hi"}]})
    assert r.status_code == 503
    assert r.json()["code"] == "NO_MODELS_CONFIGURED"


def test_chat_reports_usage(fake_client, monkeypatch):
    reported: list[dict] = []
    # main 模块以 `from app.usage import enqueue_usage` 引用，须替换 main 命名空间内的名字
    monkeypatch.setattr("app.main.enqueue_usage", reported.append)
    r = client.post("/v1/chat", json={
        "messages": [{"role": "user", "content": "hi"}],
        "business": "bid-compare",
    })
    assert r.status_code == 200
    assert reported[0]["business"] == "bid-compare"
    assert reported[0]["usedConfig"] == "fake"
    assert reported[0]["totalTokens"] == 15
    assert reported[0]["success"] is True


def test_stream_delta_and_done(fake_client):
    fake_client.stream_events = [
        {"type": "delta", "text": "你"},
        {"type": "delta", "text": "好"},
        {
            "type": "done",
            "finish_reason": "stop",
            "usage": {"prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15},
            "used_config": "fake",
            "used_model": "fake-model",
            "attempts": 1,
            "latency_seconds": 0.01,
            "circuit_breaker_state": "closed",
        },
    ]
    r = client.post("/v1/chat/stream", json={"messages": [{"role": "user", "content": "hi"}]})
    assert r.status_code == 200
    assert r.headers["content-type"].startswith("text/event-stream")
    lines = [ln for ln in r.text.splitlines() if ln.startswith("data: ")]
    payloads = [line[6:] for line in lines]
    assert '"type": "delta"' in payloads[0] and '"text": "你"' in payloads[0]
    assert '"type": "done"' in payloads[-1] and '"finishReason": "stop"' in payloads[-1]


def test_stream_error_before_first_delta(fake_client):
    fake_client.error = ProviderAuthError("bad key")
    r = client.post("/v1/chat/stream", json={"messages": [{"role": "user", "content": "hi"}]})
    assert r.status_code == 200
    assert r.headers["content-type"].startswith("text/event-stream")
    assert '"type": "error"' in r.text
    assert '"PROVIDER_AUTH"' in r.text


def test_stream_failed_after_partial(fake_client):
    fake_client.stream_events = [
        {"type": "delta", "text": "部分"},
        {
            "type": "stream_failed",
            "text": "部分",
            "finish_reason": None,
            "error": {"type": "LLMStreamError", "message": "中断"},
            "used_config": "fake",
            "used_model": "fake-model",
            "attempts": 2,
            "latency_seconds": 1.2,
            "circuit_breaker_state": "closed",
        },
    ]
    r = client.post("/v1/chat/stream", json={"messages": [{"role": "user", "content": "hi"}]})
    assert r.status_code == 200
    assert '"type": "stream_failed"' in r.text
    assert '"text": "部分"' in r.text
