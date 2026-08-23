from fastapi.testclient import TestClient
from app.main import app


def test_health_requires_key():
    client = TestClient(app)
    assert client.get("/health").status_code == 401


def test_health_with_key():
    client = TestClient(app)
    resp = client.get("/health", headers={"X-Meeting-Bot-Key": "dev-key"})
    assert resp.status_code == 200
