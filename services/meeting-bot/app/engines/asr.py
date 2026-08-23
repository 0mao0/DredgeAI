from dataclasses import dataclass


@dataclass
class AsrResult:
    text: str


class AsrEngine:
    def transcribe(self, audio_bytes: bytes, sample_rate: int = 16000) -> AsrResult:
        raise NotImplementedError


class MockAsrEngine(AsrEngine):
    def transcribe(self, audio_bytes: bytes, sample_rate: int = 16000) -> AsrResult:
        return AsrResult(text="这是模拟转写文本")


def get_asr_engine(engine_name: str) -> AsrEngine:
    if engine_name == "firered":
        from .firered_asr import FireRedAsrEngine

        from app.settings import settings

        return FireRedAsrEngine(model_dir=settings.model_dir, device=settings.asr_device)
    return MockAsrEngine()
