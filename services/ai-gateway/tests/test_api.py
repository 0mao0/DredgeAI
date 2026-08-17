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
