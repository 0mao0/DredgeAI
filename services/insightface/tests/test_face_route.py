import os

os.environ["FACE_ENGINE"] = "mock"

from fastapi.testclient import TestClient

from app.main import app


class FakeFaceEngine:
    def __init__(self):
        self.enrolled = []

    def recognize(self, image_bytes):
        from app.engines.face import FaceMatch
        return [FaceMatch(worker_id="w1", name="张三", confidence=0.95, bbox=[1, 2, 3, 4])]

    def enroll(self, worker_id, image_bytes, name=""):
        self.enrolled.append((worker_id, name))


def test_recognize():
    with TestClient(app) as client:
        app.state.face_engine = FakeFaceEngine()
        resp = client.post("/recognize", headers={"X-Meeting-Bot-Key": "dev-key"},
                           files={"image": ("m.jpg", b"jpg", "image/jpeg")})
    assert resp.status_code == 200
    assert resp.json()["faces"][0]["workerId"] == "w1"


def test_enroll():
    fake = FakeFaceEngine()
    with TestClient(app) as client:
        app.state.face_engine = fake
        resp = client.post("/enroll", headers={"X-Meeting-Bot-Key": "dev-key"},
                           data={"worker_id": "w2", "name": "李四"},
                           files={"image": ("m.jpg", b"jpg", "image/jpeg")})
    assert resp.status_code == 200
    assert fake.enrolled == [("w2", "李四")]
