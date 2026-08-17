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
