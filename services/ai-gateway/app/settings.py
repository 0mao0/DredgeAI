"""集中配置：全部经 pydantic-settings 管理，环境变量前缀 AI_GATEWAY_。"""
from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


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
