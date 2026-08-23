class TtsEngine:
    def synthesize(self, text: str) -> bytes:
        raise NotImplementedError


class MockTtsEngine(TtsEngine):
    def synthesize(self, text: str) -> bytes:
        return b"RIFF-fake-wav-" + text.encode("utf-8")


def get_tts_engine(engine_name: str) -> TtsEngine:
    if engine_name == "firered":
        from .firered_tts import FireRedTtsEngine
        return FireRedTtsEngine()
    return MockTtsEngine()
