"""FireRedASR-AED-L 真实引擎（权重本地化：models/fireredasr-aed-l/）。"""

from __future__ import annotations

import os
import tempfile
import threading

from app.engines.asr import AsrEngine, AsrResult
from app.engines.audio import split_wav_16k_mono, to_wav_16k_mono


class FireRedAsrEngine(AsrEngine):
    """封装 fireredasr pypi 包：权重由 deploy 脚本下载到 models/fireredasr-aed-l/。

    显存策略：默认 CPU 推理（晨会转写为后台任务，RTF 可接受）；
    设置 MEETING_BOT_ASR_DEVICE=cuda 且本机有 GPU 时走 GPU。
    """

    def __init__(self, model_dir: str = "models", device: str = "auto"):
        self._model_dir = os.path.join(os.path.abspath(model_dir), "fireredasr-aed-l")
        self._device = device
        self._model = None
        self._lock = threading.Lock()

    def _ensure_model(self):
        if self._model is not None:
            return self._model
        with self._lock:
            if self._model is not None:
                return self._model
            try:
                # torch>=2.6 默认 weights_only=True，旧检查点含 argparse.Namespace，需显式放行
                import argparse

                import torch

                torch.serialization.add_safe_globals([argparse.Namespace])

                from fireredasr.data.asr_feat import ASRFeatExtractor
                from fireredasr.models.fireredasr import FireRedAsr, load_fireredasr_aed_model
                from fireredasr.tokenizer.aed_tokenizer import ChineseCharEnglishSpmTokenizer
            except ImportError as exc:
                raise RuntimeError(
                    "fireredasr 未安装，请运行 uv sync --group models 后重试"
                ) from exc
            for required in ("cmvn.ark", "model.pth.tar", "dict.txt", "train_bpe1000.model"):
                if not os.path.exists(os.path.join(self._model_dir, required)):
                    raise RuntimeError(
                        f"缺少 FireRedASR 权重 {required}（{self._model_dir}），"
                        "请先运行 scripts/deploy-meeting-bot.ps1"
                    )
            try:
                feat_extractor = ASRFeatExtractor(os.path.join(self._model_dir, "cmvn.ark"))
                model = load_fireredasr_aed_model(os.path.join(self._model_dir, "model.pth.tar"))
                tokenizer = ChineseCharEnglishSpmTokenizer(
                    os.path.join(self._model_dir, "dict.txt"),
                    os.path.join(self._model_dir, "train_bpe1000.model"),
                )
                self._model = FireRedAsr("aed", feat_extractor, model, tokenizer)
            except Exception as exc:
                raise RuntimeError(
                    "FireRedASR-AED-L 权重加载失败：" + str(exc)
                ) from exc
            return self._model

    def _use_gpu(self) -> bool:
        if self._device == "cpu":
            return False
        try:
            import torch
            return torch.cuda.is_available()
        except Exception:
            return False

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
                results = model.transcribe(
                    [f"utt_{idx}"],
                    [wav_path],
                    {"use_gpu": 1 if self._use_gpu() else 0, "beam_size": 3},
                )
                text = (results[0].get("text") or "").strip()
                if text:
                    texts.append(text)
        return AsrResult(text="".join(texts))
