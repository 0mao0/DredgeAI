"""占位引擎：Task 2 替换为真实 SenseVoice 实现。"""


class AsrResult:
    def __init__(self, text: str):
        self.text = text


class SenseVoiceAsrEngine:
    def __init__(self, model_dir: str = "models", device: str = "cpu"):
        self._model = None

    @property
    def loaded(self) -> bool:
        return self._model is not None

    def transcribe(self, audio_bytes: bytes, sample_rate: int = 16000) -> AsrResult:
        raise NotImplementedError("Task 2 实现")
