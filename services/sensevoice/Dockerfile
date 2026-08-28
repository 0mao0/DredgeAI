# SenseVoice-Small ASR 模型服务
# 权重烧入镜像：构建时从 ModelScope 下载 SenseVoiceSmall 模型
FROM registry.cn-shanghai.aliyuncs.com/cyf7950/cuda:12.6.2-cudnn-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

RUN sed -i 's|deb http://archive.ubuntu.com/ubuntu|deb https://mirrors.aliyun.com/ubuntu|g; s|deb http://security.ubuntu.com/ubuntu|deb https://mirrors.aliyun.com/ubuntu|g' /etc/apt/sources.list \
    && apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates ffmpeg libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && curl -LsSf https://astral.sh/uv/install.sh | env UV_INSTALLER_GITHUB_BASE_URL="https://ghfast.top/https://github.com" sh

WORKDIR /app

COPY pyproject.toml uv.lock .python-version ./
RUN uv sync --frozen --no-dev

COPY scripts ./scripts
ENV MODELSCOPE_CACHE=/tmp/ms_cache
RUN .venv/bin/python scripts/download_model.py /app/models

COPY app ./app

ENV MODEL_DIR=/app/models \
    ASR_DEVICE=cpu \
    MEETING_BOT_KEY=dev-key

EXPOSE 8102
CMD [".venv/bin/python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8102"]
