import os
# 测试不校验网关令牌，避免 .env 中真实 token 导致所有接口 401
os.environ.setdefault("AI_GATEWAY_API_TOKEN", "")

import pytest
from ai_inference import ChatResult


class FakeLLMClient:
    """与 ai_inference.LLMClient 的 achat_result / achat_stream_events 契约一致的最小替身。"""

    def __init__(self, *, result: ChatResult | None = None, stream_events: list[dict] | None = None, error: Exception | None = None):
        self.result = result or ChatResult(
            text="ok",
            finish_reason="stop",
            usage={"prompt_tokens": 10, "completion_tokens": 5, "total_tokens": 15},
            attempts=1,
            latency_seconds=0.01,
            used_config="fake",
            used_model="fake-model",
            circuit_breaker_state="closed",
        )
        self.stream_events = stream_events
        self.error = error
        self.calls: list[dict] = []

    @property
    def configs(self) -> list[dict]:
        return [{
            "name": "fake",
            "model": "fake-model",
            "api_key": "***",
            "base_url": "http://fake/v1",
            "enabled": True,
            "priority": 1,
        }]

    async def achat_result(self, messages, temperature=None, model=None, mode="instruct",
                           config_name=None, max_tokens=None, tools=None):
        self.calls.append({"messages": messages, "mode": mode, "config_name": config_name})
        if self.error is not None:
            raise self.error
        return self.result

    async def achat_stream_events(self, messages, temperature=None, model=None, mode="instruct",
                                  config_name=None, max_tokens=None, tools=None):
        self.calls.append({"messages": messages, "mode": mode, "config_name": config_name})
        if self.error is not None:
            raise self.error
        for event in self.stream_events or []:
            yield event


@pytest.fixture()
def fake_client(monkeypatch):
    client = FakeLLMClient()
    # 网关自持 client 实例（模块级 _client），测试只替换访问器
    monkeypatch.setattr("app.main.llm_client", lambda: client)
    return client
