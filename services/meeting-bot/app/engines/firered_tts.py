"""FireRedTTS-1S 引擎：以常驻子进程方式调用独立 3.10 venv 中的 worker。"""

from __future__ import annotations

import json
import os
import subprocess
import tempfile
import threading
import uuid

from app.engines.tts import TtsEngine


class FireRedTtsEngine(TtsEngine):
    def __init__(
        self,
        model_dir: str = "models",
        venv_python: str = "",
        pretrained_dir: str = "",
        prompt_wav: str = "",
        prompt_text: str = "",
        device: str = "auto",
    ):
        self._model_dir = os.path.abspath(model_dir)
        self._venv_python = venv_python
        self._device = device

        service_root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
        self._worker_py = os.path.join(os.path.dirname(__file__), "tts_worker.py")

        # FireRedTTS 源码默认装到 third_party/FireRedTTS（deploy 脚本负责）
        third_party = os.path.join(service_root, "third_party", "FireRedTTS")
        self._tts_root = third_party
        self._config = os.path.join(third_party, "configs", "config_24k.json")
        self._pretrained = pretrained_dir or os.path.join(self._model_dir, "fireredtts", "pretrained_models")
        self._prompt_wav = prompt_wav or os.path.join(third_party, "examples", "prompt_1.wav")
        self._prompt_text = prompt_text or ""

        if not venv_python:
            candidate = os.path.join(service_root, ".venv-tts", "Scripts", "python.exe")
            if os.path.exists(candidate):
                self._venv_python = candidate

        self._proc: subprocess.Popen | None = None
        self._lock = threading.Lock()

    def _resolve_python(self) -> str:
        if self._venv_python and os.path.exists(self._venv_python):
            return self._venv_python
        raise RuntimeError(
            "未找到 FireRedTTS 专用 Python 3.10 环境，请先运行 scripts/deploy-meeting-bot.ps1"
        )

    def _ensure_worker(self) -> subprocess.Popen:
        if self._proc is not None and self._proc.poll() is None:
            return self._proc
        with self._lock:
            if self._proc is not None and self._proc.poll() is None:
                return self._proc
            for path, label in (
                (self._config, "FireRedTTS 配置"),
                (self._pretrained, "FireRedTTS 权重"),
                (self._prompt_wav, "参考音色"),
            ):
                if not os.path.exists(path):
                    raise RuntimeError(f"缺少{label}: {path}（请先运行 scripts/deploy-meeting-bot.ps1）")
            env = os.environ.copy()
            env.update(
                {
                    "FIREDREDTTS_CONFIG": self._config,
                    "FIREDREDTTS_ROOT": self._tts_root,
                    "FIREDREDTTS_PRETRAINED": self._pretrained,
                    "FIREDREDTTS_PROMPT_WAV": self._prompt_wav,
                    "FIREDREDTTS_PROMPT_TEXT": self._prompt_text,
                    "FIREDREDTTS_DEVICE": self._device,
                    "PYTHONUNBUFFERED": "1",
                    "PYTHONIOENCODING": "utf-8",
                }
            )
            # stderr 写入日志文件：避免管道缓冲写满导致 worker 死锁，同时便于排查
            os.makedirs(os.path.join(self._model_dir, "tts_cache"), exist_ok=True)
            stderr_log = open(
                os.path.join(self._model_dir, "tts_cache", "worker.log"), "a", encoding="utf-8"
            )
            self._proc = subprocess.Popen(
                [self._resolve_python(), "-u", self._worker_py],
                stdin=subprocess.PIPE,
                stdout=subprocess.PIPE,
                stderr=stderr_log,
                text=True,
                encoding="utf-8",
                env=env,
            )
            ready = self._proc.stdout.readline().strip()
            if ready != "READY":
                raise RuntimeError(f"FireRedTTS worker 启动失败: {ready or ''}（详见 models/tts_cache/worker.log）")
            return self._proc

    def synthesize(self, text: str) -> bytes:
        proc = self._ensure_worker()
        with self._lock:
            req_id = uuid.uuid4().hex
            os.makedirs(os.path.join(self._model_dir, "tts_cache"), exist_ok=True)
            out_path = os.path.join(self._model_dir, "tts_cache", f"{req_id}.wav")
            payload = json.dumps({"id": req_id, "text": text, "out": out_path}, ensure_ascii=False)
            proc.stdin.write(payload + "\n")
            proc.stdin.flush()
            line = proc.stdout.readline().strip()
            if not line:
                raise RuntimeError("FireRedTTS worker 已退出，无法合成语音")
            resp = json.loads(line)
            if resp.get("error"):
                raise RuntimeError(f"FireRedTTS 合成失败: {resp['error']}")
            try:
                with open(resp["out"], "rb") as f:
                    return f.read()
            finally:
                if os.path.exists(resp["out"]):
                    os.unlink(resp["out"])
