import time

from fastapi.testclient import TestClient

from app.main import app


class FakeResponse:
    status_code = 200

    def json(self):
        return {"text": "长音频转写结果"}


class FakeAsyncClient:
    def __init__(self, *args, **kwargs):
        pass

    async def __aenter__(self):
        return self

    async def __aexit__(self, *exc):
        return False

    async def post(self, url, **kwargs):
        return FakeResponse()


def test_transcribe_job(monkeypatch):
    monkeypatch.setattr("app.routes.transcribe.httpx.AsyncClient", FakeAsyncClient)
    with TestClient(app) as client:
        resp = client.post("/transcribe", headers={"X-Meeting-Bot-Key": "dev-key"},
                           files={"audio": ("q.wav", b"data", "audio/wav")})
        job_id = resp.json()["job_id"]
        for _ in range(50):
            r = client.get(f"/transcribe/{job_id}", headers={"X-Meeting-Bot-Key": "dev-key"})
            status = r.json()["status"]
            if status == "done":
                assert r.json()["text"] == "长音频转写结果"
                return
            if status == "error":
                raise AssertionError(r.json()["text"])
            time.sleep(0.1)
    raise AssertionError("transcribe job 未完成")
