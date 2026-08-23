"""FireRedTTS 常驻 worker（在独立 Python 3.10 venv 中运行）。

协议：stdin/stdout 逐行 JSON。引擎进程负责拉起本脚本并保持通信。
  in : {"id": "...", "text": "...", "out": "<目标 wav 路径>"}
  out: {"id": "...", "out": "<wav 路径>"} 或 {"id": "...", "error": "..."}

环境变量：
  FIREDREDTTS_CONFIG      配置文件路径（config_24k.json）
  FIREDREDTTS_PRETRAINED  pretrained_models 目录
  FIREDREDTTS_PROMPT_WAV  参考音色 wav（3~10s）
  FIREDREDTTS_PROMPT_TEXT 参考音色对应文本
  FIREDREDTTS_DEVICE      cuda | cpu（默认自动）
"""

from __future__ import annotations

import json
import os
import sys
import traceback

import torch


def main() -> None:
    # 配置中的 codebook_path 等为相对路径（相对 FireRedTTS 源码根），先切目录再加载
    # FireRedTTS 源码会往 stdout 打印“Removing weight norm...”“---text:...”等噪音，
    # 若混入协议流会破坏 JSON 行协议；三方库 print 全部导向 stderr，协议输出走 real_stdout。
    real_stdout = sys.stdout
    sys.stdout = sys.stderr
    tts_root = os.environ.get("FIREDREDTTS_ROOT", "")
    if tts_root:
        os.chdir(tts_root)

    config_path = os.environ.get("FIREDREDTTS_CONFIG", "")
    pretrained = os.environ.get("FIREDREDTTS_PRETRAINED", "")
    prompt_wav = os.environ.get("FIREDREDTTS_PROMPT_WAV", "")
    prompt_text = os.environ.get("FIREDREDTTS_PROMPT_TEXT", "")
    device = os.environ.get("FIREDREDTTS_DEVICE", "cuda" if torch.cuda.is_available() else "cpu")
    if device == "auto":
        device = "cuda" if torch.cuda.is_available() else "cpu"
    fp16 = os.environ.get("FIREDREDTTS_FP16", "1" if device == "cuda" else "0")
    os.environ["FIREDREDTTS_FP16"] = fp16

    for path, label in ((config_path, "配置"), (pretrained, "权重"), (prompt_wav, "参考音色")):
        if not os.path.exists(path):
            print(json.dumps({"fatal": f"缺少{label}文件: {path}"}), flush=True)
            sys.exit(2)

    try:
        from fireredtts.fireredtts import FireRedTTS

        tts = FireRedTTS(config_path=config_path, pretrained_path=pretrained, device=device)
    except Exception as exc:
        traceback.print_exc(file=sys.stderr)
        print(json.dumps({"fatal": f"FireRedTTS 加载失败: {exc}"}), flush=True, file=real_stdout)
        sys.exit(2)

    print("READY", flush=True, file=real_stdout)
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            req = json.loads(line)
            req_id = req.get("id", "")
            text = req.get("text", "")
            out_path = req.get("out", "")
            if not text or not out_path:
                print(json.dumps({"id": req_id, "error": "text/out 必填"}), flush=True, file=real_stdout)
                continue
            rec_wavs = tts.synthesize(prompt_wav=prompt_wav, text=text, lang="zh")
            if rec_wavs is None:
                print(json.dumps({"id": req_id, "error": "合成返回空（文本分句失败）"}), flush=True, file=real_stdout)
                continue
            rec_wavs = rec_wavs.detach().cpu()
            import torchaudio

            torchaudio.save(out_path, rec_wavs.float(), 24000)
            print(json.dumps({"id": req_id, "out": out_path}), flush=True, file=real_stdout)
        except Exception as exc:
            traceback.print_exc(file=sys.stderr)
            print(json.dumps({"id": req.get("id", ""), "error": str(exc)}), flush=True, file=real_stdout)


if __name__ == "__main__":
    main()
