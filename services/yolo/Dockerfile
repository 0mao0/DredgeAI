# YOLOv8n 人数统计模型服务（CPU 推理）
# 权重挂载 D:/AI/AImodles/models -> /app/models
FROM python:3.12-slim

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    UV_HTTP_TIMEOUT=600 \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

RUN sed -i 's|deb.debian.org|mirrors.aliyun.com|g; s|security.debian.org|mirrors.aliyun.com|g' /etc/apt/sources.list.d/debian.sources \
    && apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates libgl1 libglib2.0-0 libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && pip install --no-cache-dir -i https://mirrors.aliyun.com/pypi/simple/ "uv==0.12.5"

WORKDIR /app

COPY pyproject.toml uv.lock .python-version ./
RUN uv sync --frozen --no-dev

COPY app ./app

ENV MODEL_DIR=/app/models \
    COUNT_DEVICE=cpu \
    MEETING_BOT_KEY=dev-key

EXPOSE 8104
CMD [".venv/bin/python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8104"]
