import os

os.environ["COUNT_ENGINE"] = "mock"

from fastapi.testclient import TestClient

from app.main import app


class FakeCountEngine:
    def count(self, image_bytes):
        return 2


def test_count():
    with TestClient(app) as client:
        app.state.count_engine = FakeCountEngine()
        resp = client.post("/count", headers={"X-Meeting-Bot-Key": "dev-key"},
                           files={"image": ("m.jpg", b"jpg", "image/jpeg")})
    assert resp.status_code == 200
    assert resp.json()["count"] == 2
