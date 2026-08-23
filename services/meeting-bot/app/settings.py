from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", env_file_encoding="utf-8", extra="ignore")

    meeting_bot_key: str = "dev-key"
    asr_engine: str = "mock"       # mock | firered
    tts_engine: str = "mock"       # mock | firered
    face_engine: str = "mock"      # mock | insightface
    count_engine: str = "mock"     # mock | yolo
    model_dir: str = "models"
    # ---- 真实引擎配置 ----
    asr_device: str = "auto"          # auto | cuda | cpu（auto=本机有 GPU 则 cuda）
    tts_device: str = "auto"          # auto | cuda | cpu
    tts_venv_python: str = ""         # FireRedTTS 3.10 venv 的 python（默认 .venv-tts）
    tts_pretrained_dir: str = ""      # 默认 <model_dir>/fireredtts-1s/pretrained_models
    tts_prompt_wav: str = ""          # 默认 FireRedTTS 自带示例音色
    tts_prompt_text: str = ""         # 示例音色对应文本
    face_recognize_threshold: float = 0.55
    face_providers: str = "cpu"       # cpu | gpu
    count_device: str = "cpu"         # cpu | cuda


settings = Settings()
