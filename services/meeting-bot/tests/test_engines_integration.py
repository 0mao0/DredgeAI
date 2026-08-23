"""真实模型引擎集成测试（默认跳过，需显式开启）。

运行方式：
    $env:MEETING_BOT_INTEGRATION=1
    uv run pytest tests/test_engines_integration.py -v

依赖：
    - scripts/deploy-meeting-bot.ps1 已完成（模型已就位）
    - data/meeting-bot/samples/meeting.jpg（含人照片，可选，缺失时跳过视觉断言）
"""

from __future__ import annotations

import os
import wave

import pytest

from app.engines.asr import get_asr_engine
from app.engines.count import get_count_engine
from app.engines.face import get_face_engine
from app.engines.tts import get_tts_engine


INTEGRATION = os.environ.get("MEETING_BOT_INTEGRATION", "0") == "1"
pytestmark = pytest.mark.skipif(
    not INTEGRATION,
    reason="真实模型集成测试：设置 MEETING_BOT_INTEGRATION=1 后运行",
)


def _sample_image() -> bytes | None:
    for rel in (
        "../../../data/meeting-bot/samples/meeting.jpg",
        "../../../data/meeting-bot/workers-sample/01.jpg",
    ):
        p = os.path.join(os.path.dirname(__file__), rel)
        if os.path.exists(p):
            with open(p, "rb") as f:
                return f.read()
    return None


def _tone_wav(seconds: float = 2.0) -> bytes:
    import io

    import numpy as np

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


def test_tts_firered_returns_wav():
    engine = get_tts_engine("firered")
    audio = engine.synthesize("今天的安全交底重点有三条。")
    assert audio[:4] == b"RIFF"
    assert len(audio) > 1024


def test_asr_firered_returns_text():
    engine = get_asr_engine("firered")
    result = engine.transcribe(_tone_wav(2.0))
    assert isinstance(result.text, str)


def test_count_yolo_on_photo():
    img = _sample_image()
    if img is None:
        pytest.skip("缺少样例照片 data/meeting-bot/samples/meeting.jpg")
    engine = get_count_engine("yolo")
    assert engine.count(img) >= 1


def test_face_insightface_recognize_returns_list():
    img = _sample_image()
    if img is None:
        pytest.skip("缺少样例照片 data/meeting-bot/samples/meeting.jpg")
    engine = get_face_engine("insightface")
    faces = engine.recognize(img)
    assert isinstance(faces, list)
