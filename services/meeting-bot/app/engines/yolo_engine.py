"""YOLOv8n 人数统计引擎。"""

from __future__ import annotations

import os

import numpy as np

from app.engines.count import CountEngine


def _decode_image(image_bytes: bytes):
    import cv2

    img = cv2.imdecode(np.frombuffer(image_bytes, np.uint8), cv2.IMREAD_COLOR)
    if img is None:
        raise ValueError("图片解码失败")
    return img


class YoloCountEngine(CountEngine):
    def __init__(self, model_dir: str = "models", device: str = "cpu"):
        self._model_path = os.path.join(os.path.abspath(model_dir), "yolov8n.pt")
        if not os.path.exists(self._model_path):
            raise RuntimeError(
                f"缺少 YOLO 权重 {self._model_path}，请先运行 scripts/deploy-meeting-bot.ps1"
            )
        try:
            from ultralytics import YOLO
        except ImportError as exc:
            raise RuntimeError("ultralytics 未安装，请运行 uv sync --group models") from exc
        self._model = YOLO(self._model_path)
        self._device = device

    def count(self, image_bytes: bytes) -> int:
        img = _decode_image(image_bytes)
        results = self._model.predict(img, verbose=False, device=self._device)
        if not results or results[0].boxes is None:
            return 0
        boxes = results[0].boxes
        cls = boxes.cls
        if cls is None:
            return 0
        return int((cls == 0).sum().item())
