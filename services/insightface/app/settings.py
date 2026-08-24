from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    meeting_bot_key: str = "dev-key"
    face_engine: str = "insightface"  # mock | insightface
    model_dir: str = "models"
    face_recognize_threshold: float = 0.55
    face_providers: str = "cpu"  # cpu | gpu


settings = Settings()
