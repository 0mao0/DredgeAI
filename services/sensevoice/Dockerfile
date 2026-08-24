# SenseVoice-Small ASR 模型服务
# 权重不烧入镜像：运行时挂载 D:/AI/AImodles/models -> /app/models
FROM nvidia/cuda:12.6.2-cudnn-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

RUN apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates ffmpeg libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && curl -LsSf https://astral.sh/uv/install.sh | sh

WORKDIR /app

COPY pyproject.toml uv.lock .python-version ./
RUN uv sync --frozen --no-dev

COPY app ./app

ENV MODEL_DIR=/app/models \
    ASR_DEVICE=cpu \
    MEETING_BOT_KEY=dev-key

EXPOSE 8102
CMD [".venv/bin/python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8102"]
