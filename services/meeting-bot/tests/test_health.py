from fastapi.testclient import TestClient
from app.main import app


def test_health():
    client = TestClient(app)
    resp = client.get("/health", headers={"X-Meeting-Bot-Key": "dev-key"})
    assert resp.status_code == 200
    assert resp.json() == {"status": "ok"}
