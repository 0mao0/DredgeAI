"""音频工具：统一转成 16kHz 单声道 PCM WAV，供 ASR 引擎使用。"""

from __future__ import annotations

import io
import shutil
import subprocess
import wave


class AudioConvertError(RuntimeError):
    pass


def to_wav_16k_mono(data: bytes) -> bytes:
    """任意浏览器录音（webm/opus/mp3/wav…）→ 16kHz 单声道 PCM WAV。

    依赖 ffmpeg；缺失时给出可读错误。
    """
    ffmpeg = shutil.which("ffmpeg")
    if not ffmpeg:
        raise AudioConvertError(
            "未找到 ffmpeg，无法将录音转为 16k WAV。请先安装 ffmpeg（choco install ffmpeg）"
        )
    proc = subprocess.run(
        [
            ffmpeg, "-nostdin", "-loglevel", "error",
            "-i", "pipe:0",
            "-ar", "16000", "-ac", "1", "-c:a", "pcm_s16le", "-f", "wav", "pipe:1",
        ],
        input=data,
        capture_output=True,
        timeout=120,
    )
    if proc.returncode != 0:
        raise AudioConvertError(
            "音频转码失败: " + proc.stderr.decode("utf-8", errors="replace")[:500]
        )
    return proc.stdout


def split_wav_16k_mono(data: bytes, chunk_seconds: int = 50) -> list[bytes]:
    """把 16k 单声道 WAV 切成若干 <=chunk_seconds 秒的 WAV 片段。

    FireRedASR-AED 支持 <=60s 输入，超过会导致幻觉/位置编码错误；
    晨会全程录音在此切块后逐段转写。
    """
    with wave.open(io.BytesIO(data), "rb") as wf:
        params = wf.getparams()
        if params.framerate != 16000 or params.nchannels != 1:
            raise AudioConvertError(f"期望 16k 单声道 WAV，实际 {params.framerate}Hz/{params.nchannels}ch")
        sample_width = params.sampwidth
        total_frames = params.nframes
        chunk_frames = 16000 * chunk_seconds
        chunks: list[bytes] = []
        while True:
            frames = wf.readframes(chunk_frames)
            if not frames:
                break
            buf = io.BytesIO()
            with wave.open(buf, "wb") as out:
                out.setnchannels(1)
                out.setsampwidth(sample_width)
                out.setframerate(16000)
                out.writeframes(frames)
            chunks.append(buf.getvalue())
            if wf.tell() >= total_frames:
                break
    return chunks
