from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    meeting_bot_key: str = "dev-key"
    # 模型服务地址（compose 内走服务名；裸跑开发可改 localhost）
    sensevoice_url: str = "http://sensevoice:8102"
    cosyvoice_url: str = "http://cosyvoice:8000"
    insightface_url: str = "http://insightface:8103"
    yolo_url: str = "http://yolo:8104"
    tts_voice_id: str = "zh-male-news"
    transcribe_max_concurrency: int = 1


settings = Settings()
