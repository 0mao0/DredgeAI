"""集中配置：全部经 pydantic-settings 管理，环境变量前缀 AI_GATEWAY_。"""
from functools import lru_cache
from pathlib import Path

from dotenv import load_dotenv
from pydantic_settings import BaseSettings, SettingsConfigDict


_REPO_ROOT = Path(__file__).resolve().parents[3]
_ENV_FILE = _REPO_ROOT / ".env"
if _ENV_FILE.exists():
    # 与后端约定一致：仓库根 .env 是统一密钥入口（LLM_CONFIGS / AI_GATEWAY_* 均由此注入）
    load_dotenv(_ENV_FILE)


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_prefix="AI_GATEWAY_")

    # 入站校验令牌；空表示关闭（开发环境）
    api_token: str = ""
    # 用量上报（ABP ingest 端点）
    usage_report_url: str = "http://localhost:44361/api/ai-gateway/usage-records"
    usage_report_enabled: bool = True
    ingest_token: str = ""


@lru_cache
def get_settings() -> Settings:
    """进程级配置单例；调用时读取，便于测试 monkeypatch 后 cache_clear。"""
    return Settings()
