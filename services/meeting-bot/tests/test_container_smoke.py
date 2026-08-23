"""容器化模型服务冒烟：对运行中的 meeting-bot（裸进程或容器）做全 API 回归。

运行（容器场景）：
    $env:MEETING_BOT_BASE_URL="http://localhost:8101"
    uv run pytest tests/test_container_smoke.py -v
依赖宿主样例照片 data/meeting-bot/samples/meeting.jpg（缺失时视觉用例跳过）。
"""

import io
import os
import wave

import httpx
import numpy as np
import pytest

BASE = os.environ.get("MEETING_BOT_BASE_URL", "http://localhost:8101").rstrip("/")
KEY = os.environ.get("MEETING_BOT_KEY", "dev-key")
HEADERS = {"X-Meeting-Bot-Key": KEY}

pytestmark = pytest.mark.skipif(
    not os.environ.get("MEETING_BOT_BASE_URL"),
    reason="设置 MEETING_BOT_BASE_URL 指向被测服务",
)


def _sample_image() -> bytes | None:
    p = os.path.abspath(
        os.path.join(os.path.dirname(__file__), "..", "..", "..", "data", "meeting-bot", "samples", "meeting.jpg")
    )
    return open(p, "rb").read() if os.path.exists(p) else None


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


def test_health():
    r = httpx.get(f"{BASE}/health", headers=HEADERS, timeout=10)
    assert r.status_code == 200
    assert r.json()["status"] == "ok"


def test_count():
    img = _sample_image()
    if img is None:
        pytest.skip("缺少样例照片 data/meeting-bot/samples/meeting.jpg")
    r = httpx.post(
        f"{BASE}/count", headers=HEADERS, files={"image": ("m.jpg", img, "image/jpeg")}, timeout=120
    )
    assert r.status_code == 200
    assert r.json()["count"] >= 1


def test_asr():
    r = httpx.post(
        f"{BASE}/asr", headers=HEADERS, files={"audio": ("t.wav", _tone_wav(), "audio/wav")}, timeout=300
    )
    assert r.status_code == 200
    assert isinstance(r.json()["text"], str)


def test_tts():
    r = httpx.post(f"{BASE}/tts", headers=HEADERS, json={"text": "今天的安全交底重点有三条。"}, timeout=300)
    assert r.status_code == 200
    assert r.content[:4] == b"RIFF"
    assert len(r.content) > 1024


def test_recognize():
    img = _sample_image()
    if img is None:
        pytest.skip("缺少样例照片 data/meeting-bot/samples/meeting.jpg")
    r = httpx.post(
        f"{BASE}/recognize", headers=HEADERS, files={"image": ("m.jpg", img, "image/jpeg")}, timeout=120
    )
    assert r.status_code == 200
    assert isinstance(r.json()["faces"], list)
