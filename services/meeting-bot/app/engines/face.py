from dataclasses import dataclass, field


@dataclass
class FaceMatch:
    worker_id: str | None
    name: str | None
    confidence: float
    bbox: list[float] = field(default_factory=list)


class FaceEngine:
    def recognize(self, image_bytes: bytes) -> list[FaceMatch]:
        raise NotImplementedError

    def enroll(self, worker_id: str, image_bytes: bytes) -> None:
        raise NotImplementedError


class MockFaceEngine(FaceEngine):
    def recognize(self, image_bytes: bytes) -> list[FaceMatch]:
        return []

    def enroll(self, worker_id: str, image_bytes: bytes) -> None:
        return None


def get_face_engine(engine_name: str) -> FaceEngine:
    if engine_name == "insightface":
        from .insightface_engine import InsightFaceEngine
        return InsightFaceEngine()
    return MockFaceEngine()
