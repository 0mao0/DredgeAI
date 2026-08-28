# compare-algo 比标算法服务（确定性算法，无 LLM、无模型权重、无外部依赖服务）
# 内部服务：仅监听私网/内网，由 ABP 主服务调用，禁止直接暴露公网
FROM registry.cn-shanghai.aliyuncs.com/cyf7950/python:3.12-slim

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    UV_HTTP_TIMEOUT=600 \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

# lxml/bs4/datasketch 均为 manylinux wheel 自带依赖，无需额外系统库（区别于 yolo 的 OpenCV）
RUN sed -i 's|deb.debian.org|mirrors.aliyun.com|g; s|security.debian.org|mirrors.aliyun.com|g' /etc/apt/sources.list.d/debian.sources \
    && apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && pip install --no-cache-dir -i https://mirrors.aliyun.com/pypi/simple/ "uv==0.12.5"

WORKDIR /app

# 先装依赖再拷代码，利用层缓存（本服务无 .python-version / scripts / models）
COPY pyproject.toml uv.lock ./
RUN uv sync --frozen --no-dev

COPY app ./app

# 同步 def 端点跑 CPU 密集管线，GIL 下单进程无法并行（README「部署注意」）：
# 默认 2 个 uvicorn worker 横向扩展，运行时 -e WORKERS=N 覆盖；
# 内存按每 worker 约 2× 请求体上限（默认 96MB → 峰值 ~200MB）规划容器限额
ENV WORKERS=2

EXPOSE 8100
CMD ["sh", "-c", "exec .venv/bin/python -m uvicorn app.main:app --host 0.0.0.0 --port 8100 --workers $WORKERS"]
