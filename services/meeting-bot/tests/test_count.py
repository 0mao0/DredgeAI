import os

import pytest
from fastapi.testclient import TestClient

from app.main import app


def _sample_image() -> bytes | None:
    p = os.path.abspath(
        os.path.join(os.path.dirname(__file__), "..", "..", "..", "data", "meeting-bot", "samples", "meeting.jpg")
    )
    return open(p, "rb").read() if os.path.exists(p) else None


def test_count_returns_number():
    img = _sample_image()
    if img is None:
        pytest.skip("缺少样例照片 data/meeting-bot/samples/meeting.jpg")
    client = TestClient(app)
    resp = client.post("/count", headers={"X-Meeting-Bot-Key": "dev-key"},
                       files={"image": ("g.jpg", img, "image/jpeg")})
    assert resp.status_code == 200
    assert resp.json()["count"] >= 1
