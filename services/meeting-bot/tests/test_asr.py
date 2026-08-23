import io
import wave

import numpy as np
from fastapi.testclient import TestClient

from app.main import app


def _tone_wav(seconds: float = 2.0) -> bytes:
    rate = 16000
    n = int(rate * seconds)
    t = np.linspace(0, seconds, n, endpoint=False)
    pcm = (0.2 * np.sin(2 * np.pi * 440 * t) * 32767).astype(np.int16)
    buf = io.BytesIO()
    with wave.open(buf, "wb") as wf:
        wf.setnchannels(1)
        wf.setsampwidth(2)
        wf.setframerate(rate)
        wf.writeframes(pcm.tobytes())
    return buf.getvalue()


def test_asr_returns_text():
    client = TestClient(app)
    resp = client.post("/asr", headers={"X-Meeting-Bot-Key": "dev-key"},
                       files={"audio": ("q.wav", _tone_wav(), "audio/wav")})
    assert resp.status_code == 200
    assert isinstance(resp.json()["text"], str)
