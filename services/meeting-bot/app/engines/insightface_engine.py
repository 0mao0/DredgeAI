"""InsightFace（buffalo_l）人脸引擎：注册 + 识别，本地 embedding 库落盘 JSON。"""

from __future__ import annotations

import json
import os
import threading

import numpy as np

from app.engines.face import FaceEngine, FaceMatch


def _decode_image(image_bytes: bytes):
    import cv2

    img = cv2.imdecode(np.frombuffer(image_bytes, np.uint8), cv2.IMREAD_COLOR)
    if img is None:
        raise ValueError("图片解码失败")
    return img


class InsightFaceEngine(FaceEngine):
    """识别与注册共用 buffalo_l 检测+识别模型。

    识别返回的 confidence 为归一化余弦相似度（0~1）；
    注册把最大人脸 embedding 存入 models/faces.json。
    """

    def __init__(
        self,
        model_dir: str = "models",
        threshold: float = 0.55,
        providers: str = "cpu",
    ):
        self._model_dir = os.path.abspath(model_dir)
        self._threshold = threshold
        self._db_path = os.path.join(self._model_dir, "faces.json")
        self._lock = threading.Lock()

        # insightface 约定 <root>/models/<name>，root 传 models 的父目录
        model_root = os.path.dirname(self._model_dir)
        if not os.path.isdir(os.path.join(model_root, "models", "buffalo_l")):
            raise RuntimeError(
                f"缺少人脸模型 buffalo_l（{model_root}/models/buffalo_l），请先运行 scripts/deploy-meeting-bot.ps1"
            )
        try:
            from insightface.app import FaceAnalysis
        except ImportError as exc:
            raise RuntimeError("insightface 未安装，请运行 uv sync --group models") from exc

        if providers == "gpu":
            providers_cfg = ["CUDAExecutionProvider", "CPUExecutionProvider"]
            ctx_id = 0
        else:
            providers_cfg = ["CPUExecutionProvider"]
            ctx_id = -1
        self._app = FaceAnalysis(name="buffalo_l", root=model_root, providers=providers_cfg)
        self._app.prepare(ctx_id=ctx_id, det_size=(640, 640))
        self._db = self._load_db()

    def _load_db(self) -> dict:
        if os.path.exists(self._db_path):
            try:
                with open(self._db_path, "r", encoding="utf-8") as f:
                    return json.load(f)
            except (json.JSONDecodeError, OSError):
                return {}
        return {}

    def _save_db(self) -> None:
        os.makedirs(self._model_dir, exist_ok=True)
        tmp = self._db_path + ".tmp"
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(self._db, f, ensure_ascii=False, indent=2)
        os.replace(tmp, self._db_path)

    @staticmethod
    def _embedding(face) -> np.ndarray:
        emb = getattr(face, "normed_embedding", None)
        if emb is None:
            emb = getattr(face, "embedding", None)
        if emb is None:
            raise ValueError("检测到人脸但无法提取 embedding")
        emb = np.asarray(emb, dtype=np.float32)
        norm = np.linalg.norm(emb)
        return emb / norm if norm > 0 else emb

    def _largest_face(self, img) -> dict | None:
        faces = self._app.get(img)
        if not faces:
            return None
        largest = max(faces, key=lambda f: (f.bbox[2] - f.bbox[0]) * (f.bbox[3] - f.bbox[1]))
        return {"face": largest, "embedding": self._embedding(largest)}

    def enroll(self, worker_id: str, image_bytes: bytes, name: str = "") -> None:
        img = _decode_image(image_bytes)
        hit = self._largest_face(img)
        if hit is None:
            raise ValueError("图片中未检测到人脸，无法注册")
        with self._lock:
            self._db[worker_id] = {
                "name": name or "",
                "embedding": hit["embedding"].tolist(),
            }
            self._save_db()

    def recognize(self, image_bytes: bytes) -> list[FaceMatch]:
        img = _decode_image(image_bytes)
        faces = self._app.get(img)
        if not faces:
            return []
        matches: list[FaceMatch] = []
        with self._lock:
            db = dict(self._db)
        for face in faces:
            emb = self._embedding(face)
            best_worker: str | None = None
            best_sim = 0.0
            for worker_id, record in db.items():
                db_emb = np.asarray(record["embedding"], dtype=np.float32)
                sim = float(np.dot(emb, db_emb))
                if sim > best_sim:
                    best_sim = sim
                    best_worker = worker_id
            bbox = [float(v) for v in face.bbox]
            if best_worker is not None and best_sim >= self._threshold:
                record = db[best_worker]
                matches.append(
                    FaceMatch(
                        worker_id=best_worker,
                        name=record.get("name") or "",
                        confidence=round(best_sim, 4),
                        bbox=bbox,
                    )
                )
            else:
                matches.append(
                    FaceMatch(
                        worker_id=None,
                        name=None,
                        confidence=round(best_sim, 4),
                        bbox=bbox,
                    )
                )
        return matches
