from fastapi.testclient import TestClient

from app.main import app
from app.engines.sensevoice_asr import AsrResult


class FakeAsrEngine:
    def __init__(self):
        self.last_bytes = None

    def transcribe(self, audio_bytes: bytes, sample_rate: int = 16000) -> AsrResult:
        self.last_bytes = audio_bytes
        return AsrResult(text="测试转写")

    @property
    def loaded(self) -> bool:
        return True


def test_asr_returns_text():
    with TestClient(app) as client:
        app.state.asr_engine = FakeAsrEngine()
        resp = client.post(
            "/asr",
            headers={"X-Meeting-Bot-Key": "dev-key"},
            files={"audio": ("q.wav", b"fake-wav", "audio/wav")},
        )
    assert resp.status_code == 200
    assert resp.json()["text"] == "测试转写"


def test_health_reports_loaded():
    with TestClient(app) as client:
        app.state.asr_engine = FakeAsrEngine()
        resp = client.get("/health", headers={"X-Meeting-Bot-Key": "dev-key"})
    assert resp.status_code == 200
    assert resp.json()["model_loaded"] is True
