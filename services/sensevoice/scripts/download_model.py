"""从 ModelScope 下载 SenseVoice-Small 模型到指定目录。"""

import os
import shutil
import sys
from pathlib import Path

from modelscope import snapshot_download

MODEL_ID = "iic/SenseVoiceSmall"


def main() -> int:
    model_dir = sys.argv[1] if len(sys.argv) > 1 else "/app/models"
    target = Path(model_dir) / "SenseVoiceSmall"
    cache_dir = Path(sys.argv[2]) if len(sys.argv) > 2 else Path("/tmp/ms_cache")

    if target.is_dir():
        print(f"[download-model] 模型已存在于 {target}，跳过下载")
        return 0

    print(f"[download-model] 开始从 ModelScope 下载 {MODEL_ID} ...")
    cache_dir.mkdir(parents=True, exist_ok=True)

    cache_path = snapshot_download(MODEL_ID, cache_dir=str(cache_dir))

    target.parent.mkdir(parents=True, exist_ok=True)
    if target.exists():
        shutil.rmtree(target)

    shutil.move(str(cache_path), str(target))
    shutil.rmtree(cache_dir, ignore_errors=True)

    print(f"[download-model] 完成。模型已解压至 {target}")
    print(f"[download-model] 文件列表: {sorted(os.listdir(target))}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
