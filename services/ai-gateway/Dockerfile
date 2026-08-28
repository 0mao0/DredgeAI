# ai-gateway AI 推理网关（消费 angineer-ai-inference，转发 LLM chat / SSE；无本地模型权重）
# 内部服务：仅监听私网/内网，由 ABP 主服务调用，禁止直接暴露公网
FROM registry.cn-shanghai.aliyuncs.com/cyf7950/python:3.12-slim

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    UV_HTTP_TIMEOUT=600 \
    PATH="/root/.local/bin:/app/services/ai-gateway/.venv/bin:$PATH"

# 区别于 compare-algo：angineer-ai-inference 为 git 依赖（uv.lock 锁定 v0.1.0），
# uv sync 需要系统 git 拉取 github 仓库；其余依赖均为 manylinux wheel，无需额外系统库
RUN sed -i 's|deb.debian.org|mirrors.aliyun.com|g; s|security.debian.org|mirrors.aliyun.com|g' /etc/apt/sources.list.d/debian.sources \
    && apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates git \
    && rm -rf /var/lib/apt/lists/* \
    && pip install --no-cache-dir -i https://mirrors.aliyun.com/pypi/simple/ "uv==0.12.5"

# 容器内复刻仓库相对布局（<仓库根>/services/ai-gateway）：
# app/settings.py 以 parents[3] 定位仓库根 .env，仓库根 .env 挂载到 /app/.env
# 即可注入 LLM_CONFIGS / ANGINEER_* / AI_GATEWAY_*（-e 环境变量优先级更高）
WORKDIR /app/services/ai-gateway

# 先装依赖再拷代码，利用层缓存（本服务无 .python-version / scripts / models）
COPY pyproject.toml uv.lock ./
RUN uv sync --frozen --no-dev

COPY app ./app

# LLM 转发为全异步 I/O（chat / SSE / 用量上报），单 worker 事件循环即可承载并发，
# 区别于 compare-algo 的 CPU 密集管线默认 2 worker；运行时 -e WORKERS=N 覆盖
#（多 worker = 多进程各自持有 LLMClient 单例与上报队列，语义不变）
ENV WORKERS=1

EXPOSE 8200
CMD ["sh", "-c", "exec .venv/bin/python -m uvicorn app.main:app --host 0.0.0.0 --port 8200 --workers $WORKERS"]
