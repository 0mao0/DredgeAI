from fastapi.testclient import TestClient
from app.main import app


def test_tts_returns_audio():
    client = TestClient(app)
    resp = client.post("/tts", headers={"X-Meeting-Bot-Key": "dev-key"},
                       json={"text": "早上好"})
    assert resp.status_code == 200
    assert resp.headers["content-type"].startswith("audio/")
    assert resp.content
