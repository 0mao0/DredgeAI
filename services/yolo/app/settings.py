from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    meeting_bot_key: str = "dev-key"
    count_engine: str = "yolo"  # mock | yolo
    model_dir: str = "models"
    count_device: str = "cpu"  # cpu | cuda


settings = Settings()
