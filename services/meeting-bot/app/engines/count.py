class CountEngine:
    def count(self, image_bytes: bytes) -> int:
        raise NotImplementedError


class MockCountEngine(CountEngine):
    def count(self, image_bytes: bytes) -> int:
        return 0


def get_count_engine(engine_name: str) -> CountEngine:
    if engine_name == "yolo":
        from .yolo_engine import YoloCountEngine
        return YoloCountEngine()
    return MockCountEngine()
