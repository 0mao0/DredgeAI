# CosyVoice3-0.5B TTS 模型服务
# 权重/音色资产不烧入镜像：运行时挂载 D:/AI/AImodles/cosyvoice -> /data
FROM nvidia/cuda:12.6.2-cudnn-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

RUN apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates git ffmpeg libsndfile1 libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && curl -LsSf https://astral.sh/uv/install.sh | sh

WORKDIR /app

COPY pyproject.toml uv.lock .python-version ./
RUN uv sync --frozen --no-dev

COPY server.py ./
COPY voices_config.json ./
COPY third_party/CosyVoice ./CosyVoice

ENV COSYVOICE_DATA=/data \
    COSYVOICE_SRC=/app/CosyVoice \
    TTS_VOICE_ID=zh-male-news \
    MEETING_BOT_KEY=dev-key

EXPOSE 8000
CMD [".venv/bin/python", "-m", "uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8000"]
