from pydantic_settings import BaseSettings


class Settings(BaseSettings):
    meeting_bot_key: str = "dev-key"
    asr_engine: str = "mock"       # mock | firered
    tts_engine: str = "mock"       # mock | firered
    face_engine: str = "mock"      # mock | insightface
    count_engine: str = "mock"     # mock | yolo
    model_dir: str = "models"


settings = Settings()
