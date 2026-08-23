"""FireRedTTS worker python 候选路径：Windows 与 Linux 布局都要支持。"""

import os

from app.engines.firered_tts import _venv_python_candidates


def test_windows_layout():
    candidates = _venv_python_candidates(r"D:\svc")
    assert os.path.join(r"D:\svc", ".venv-tts", "Scripts", "python.exe") in candidates


def test_linux_layout():
    candidates = _venv_python_candidates("/app")
    assert os.path.join("/app", ".venv-tts", "bin", "python") in candidates
