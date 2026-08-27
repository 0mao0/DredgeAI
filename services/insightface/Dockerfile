# InsightFace buffalo_l 人脸模型服务
# 构建时预下载 buffalo_l 到 /app/models/buffalo_l；faces.json 可运行时挂载 /app/models 注入
FROM registry.cn-shanghai.aliyuncs.com/cyf7950/cuda:12.6.2-cudnn-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

RUN sed -i 's|deb http://archive.ubuntu.com/ubuntu|deb https://mirrors.aliyun.com/ubuntu|g; s|deb http://security.ubuntu.com/ubuntu|deb https://mirrors.aliyun.com/ubuntu|g' /etc/apt/sources.list \
    && apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates unzip libgl1 libglib2.0-0 libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && curl -LsSf https://astral.sh/uv/install.sh | env UV_INSTALLER_GITHUB_BASE_URL="https://ghfast.top/https://github.com" sh

WORKDIR /app

COPY pyproject.toml uv.lock .python-version ./
RUN uv sync --frozen --no-dev

COPY app ./app
COPY scripts ./scripts

# 预下载 buffalo_l 到 /app/models/buffalo_l，与 app/engines/insightface_engine.py 加载路径一致
RUN chmod +x scripts/download_models.sh && ./scripts/download_models.sh

ENV MODEL_DIR=/app/models \
    FACE_PROVIDERS=cpu \
    FACE_RECOGNIZE_THRESHOLD=0.55 \
    MEETING_BOT_KEY=dev-key

EXPOSE 8103
CMD [".venv/bin/python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8103"]
