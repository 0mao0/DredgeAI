import pytest

from app.engines.sensevoice_asr import SenseVoiceAsrEngine, strip_sensevoice_tags


def test_strip_tags():
    raw = "<|zh|><|NEUTRAL|><|Speech|>今天的安全交底有三条<|endoftext|>"
    assert strip_sensevoice_tags(raw) == "今天的安全交底有三条"


def test_strip_tags_empty():
    assert strip_sensevoice_tags("<|nospeech|><|zh|>") == ""


class FakeSenseVoiceModel:
    def __init__(self, texts):
        self.texts = texts
        self.calls = 0

    def generate(self, input, language="auto", use_itn=True, batch_size_s=60):
        self.calls += 1
        return [{"text": self.texts[self.calls - 1]}]


def test_transcribe_strips_and_concatenates(monkeypatch):
    engine = SenseVoiceAsrEngine(model_dir="models", device="cpu")
    fake = FakeSenseVoiceModel([
        "<|zh|><|NEUTRAL|><|Speech|>第一条内容<|endoftext|>",
        "<|zh|><|NEUTRAL|><|Speech|>第二条内容<|endoftext|>",
    ])
    engine._model = fake
    # 单测不依赖 ffmpeg：跳过转码与切块
    monkeypatch.setattr("app.engines.sensevoice_asr.to_wav_16k_mono", lambda data: data)
    monkeypatch.setattr("app.engines.sensevoice_asr.split_wav_16k_mono", lambda wav, chunk_seconds=50: [b"chunk1", b"chunk2"])
    result = engine.transcribe(b"fake-wav")
    assert result.text == "第一条内容第二条内容"
    assert fake.calls == 2


def test_transcribe_missing_model_dir_raises(monkeypatch, tmp_path):
    engine = SenseVoiceAsrEngine(model_dir=str(tmp_path), device="cpu")
    with pytest.raises(RuntimeError, match="SenseVoice-Small"):
        engine.transcribe(b"fake-wav")
