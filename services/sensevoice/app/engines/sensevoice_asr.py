"""SenseVoice-Small ASR 引擎（funasr）。"""

from __future__ import annotations

import os
import re
import tempfile
import threading

from app.engines.audio import split_wav_16k_mono, to_wav_16k_mono

_TAG_RE = re.compile(r"<\|[^|]+\|>")


def strip_sensevoice_tags(text: str) -> str:
    """去掉 SenseVoice 输出的 <|zh|><|NEUTRAL|> 等标签，保留正文。"""
    return _TAG_RE.sub("", text).strip()


class AsrResult:
    def __init__(self, text: str):
        self.text = text


class SenseVoiceAsrEngine:
    """funasr 加载 SenseVoice-Small；权重在 <model_dir>/SenseVoiceSmall。"""

    def __init__(self, model_dir: str = "models", device: str = "cpu"):
        self._model_root = os.path.join(os.path.abspath(model_dir), "SenseVoiceSmall")
        self._device = device
        self._model = None
        self._lock = threading.Lock()

    @property
    def loaded(self) -> bool:
        return self._model is not None

    def _ensure_model(self):
        if self._model is not None:
            return self._model
        with self._lock:
            if self._model is not None:
                return self._model
            if not os.path.isdir(self._model_root):
                raise RuntimeError(
                    f"缺少 SenseVoice-Small 权重（{self._model_root}），请先运行 scripts/deploy-model-services.ps1"
                )
            try:
                from funasr import AutoModel

                # 直接传本地目录：funasr 1.4.x 的 model_dir 参数不重定向下载，
                # 且 model_id 形式会把路径指到 modelscope 缓存（含中文用户名会乱码）。
                self._model = AutoModel(
                    model=self._model_root,
                    device=self._device,
                    disable_update=True,
                    disable_pbar=True,
                )
            except Exception as exc:
                raise RuntimeError("SenseVoice-Small 加载失败: " + str(exc)) from exc
            return self._model

    def transcribe(self, audio_bytes: bytes, sample_rate: int = 16000) -> AsrResult:
        model = self._ensure_model()
        wav = to_wav_16k_mono(audio_bytes)
        chunks = split_wav_16k_mono(wav, chunk_seconds=50)
        if not chunks:
            return AsrResult(text="")
        texts: list[str] = []
        with tempfile.TemporaryDirectory() as tmp:
            for idx, chunk in enumerate(chunks):
                wav_path = os.path.join(tmp, f"chunk_{idx}.wav")
                with open(wav_path, "wb") as f:
                    f.write(chunk)
                res = model.generate(
                    input=wav_path,
                    language="auto",
                    use_itn=True,
                    batch_size_s=60,
                )
                if not res:
                    continue
                text = strip_sensevoice_tags(res[0].get("text") or "")
                if text:
                    texts.append(text)
        return AsrResult(text="".join(texts))
