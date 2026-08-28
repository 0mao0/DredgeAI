"""构建时下载 CosyVoice3 模型与 wetext 资产到 /data，与 server.py 加载路径一致。

- Fun-CosyVoice3-0.5B  -> <base>/pretrained_models/Fun-CosyVoice3-0.5B
                          （server.py 默认 COSYVOICE_MODEL_DIR=<DATA>/pretrained_models/...）
- wetext               -> <base>/modelscope/hub/pengzhendong/wetext
                          （server.py 的 _pin_local_wetext 按 MODELSCOPE_CACHE/hub/<org>/<model> 查找）

已存在则跳过；默认 base=/data，可用 argv[1] 覆盖。
"""

import os
import shutil
import sys
from pathlib import Path

from modelscope import snapshot_download

TTS_MODEL_ID = "FunAudioLLM/Fun-CosyVoice3-0.5B-2512"
WETEXT_MODEL_ID = "pengzhendong/wetext"


def _fetch(model_id: str, cache_dir: Path) -> Path:
    cache_dir.mkdir(parents=True, exist_ok=True)
    return Path(snapshot_download(model_id, cache_dir=str(cache_dir)))


def _download(model_id: str, target: Path, cache_dir: Path) -> None:
    src = _fetch(model_id, cache_dir)
    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists():
        shutil.rmtree(target)
    shutil.move(str(src), str(target))
    print(f"[download-models] 完成: {target} -> {sorted(os.listdir(target))}")


def main() -> int:
    base = Path(sys.argv[1] if len(sys.argv) > 1 else "/data")
    cache = Path(sys.argv[2] if len(sys.argv) > 2 else "/tmp/ms_cache")

    tts_dir = base / "pretrained_models" / "Fun-CosyVoice3-0.5B"
    if (tts_dir / "cosyvoice3.yaml").is_file():
        print(f"[download-models] 已存在 {tts_dir}，跳过下载")
    else:
        print(f"[download-models] 从 ModelScope 下载 {TTS_MODEL_ID} ...")
        _download(TTS_MODEL_ID, tts_dir, cache)

    wetext_dir = base / "modelscope" / "hub" / "pengzhendong" / "wetext"
    has_files = wetext_dir.is_dir() and any(
        p for p in wetext_dir.iterdir() if not p.name.startswith(".")
    )
    if has_files:
        print(f"[download-models] 已存在 {wetext_dir}，跳过下载")
    else:
        print(f"[download-models] 从 ModelScope 下载 {WETEXT_MODEL_ID} ...")
        _download(WETEXT_MODEL_ID, wetext_dir, cache)

    shutil.rmtree(cache, ignore_errors=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
