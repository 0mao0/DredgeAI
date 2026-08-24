from fastapi.testclient import TestClient

from app.main import app


class FakeResponse:
    status_code = 200

    def __init__(self, payload=None, content=b""):
        self._payload = payload
        self._content = content

    def json(self):
        return self._payload

    @property
    def content(self):
        return self._content

    @property
    def headers(self):
        return {"content-type": "audio/wav"}


class FakeAsyncClient:
    def __init__(self, *args, **kwargs):
        self.calls = []

    async def __aenter__(self):
        return self

    async def __aexit__(self, *exc):
        return False

    async def post(self, url, **kwargs):
        self.calls.append((url, kwargs))
        if "/api/tts" in url:
            return FakeResponse(content=b"RIFF-cosy")
        if "/recognize" in url:
            return FakeResponse(payload={"faces": [{"workerId": "w1"}]})
        if "/enroll" in url:
            return FakeResponse(payload={"ok": True})
        if "/count" in url:
            return FakeResponse(payload={"count": 2})
        return FakeResponse(payload={"text": "转发转写"})


def _client():
    return TestClient(app)


def test_asr_forwards(monkeypatch):
    monkeypatch.setattr("app.routes.asr.httpx.AsyncClient", FakeAsyncClient)
    resp = _client().post("/asr", headers={"X-Meeting-Bot-Key": "dev-key"},
                          files={"audio": ("q.wav", b"data", "audio/wav")})
    assert resp.status_code == 200
    assert resp.json()["text"] == "转发转写"


def test_tts_forwards(monkeypatch):
    monkeypatch.setattr("app.routes.tts.httpx.AsyncClient", FakeAsyncClient)
    resp = _client().post("/tts", headers={"X-Meeting-Bot-Key": "dev-key"},
                          json={"text": "早上好"})
    assert resp.status_code == 200
    assert resp.content == b"RIFF-cosy"


def test_recognize_forwards(monkeypatch):
    monkeypatch.setattr("app.routes.face.httpx.AsyncClient", FakeAsyncClient)
    resp = _client().post("/recognize", headers={"X-Meeting-Bot-Key": "dev-key"},
                          files={"image": ("m.jpg", b"jpg", "image/jpeg")})
    assert resp.status_code == 200
    assert resp.json()["faces"][0]["workerId"] == "w1"


def test_enroll_forwards_fields(monkeypatch):
    fake = FakeAsyncClient()
    monkeypatch.setattr("app.routes.face.httpx.AsyncClient", lambda *a, **kw: fake)
    resp = _client().post("/enroll", headers={"X-Meeting-Bot-Key": "dev-key"},
                          data={"worker_id": "w2", "name": "李四"},
                          files={"image": ("m.jpg", b"jpg", "image/jpeg")})
    assert resp.status_code == 200
    sent = fake.calls[0][1]
    assert sent["data"]["worker_id"] == "w2"
    assert sent["data"]["name"] == "李四"


def test_count_forwards(monkeypatch):
    monkeypatch.setattr("app.routes.count.httpx.AsyncClient", FakeAsyncClient)
    resp = _client().post("/count", headers={"X-Meeting-Bot-Key": "dev-key"},
                          files={"image": ("m.jpg", b"jpg", "image/jpeg")})
    assert resp.status_code == 200
    assert resp.json()["count"] == 2
