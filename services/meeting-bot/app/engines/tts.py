class TtsEngine:
    def synthesize(self, text: str) -> bytes:
        raise NotImplementedError


class MockTtsEngine(TtsEngine):
    def synthesize(self, text: str) -> bytes:
        return b"RIFF-fake-wav-" + text.encode("utf-8")


def get_tts_engine(engine_name: str) -> TtsEngine:
    if engine_name == "firered":
        from .firered_tts import FireRedTtsEngine

        from app.settings import settings

        return FireRedTtsEngine(
            model_dir=settings.model_dir,
            venv_python=settings.tts_venv_python,
            pretrained_dir=settings.tts_pretrained_dir,
            prompt_wav=settings.tts_prompt_wav,
            prompt_text=settings.tts_prompt_text,
            device=settings.tts_device,
        )
    return MockTtsEngine()
