# CosyVoice3-0.5B TTS 模型服务
# 构建时预下载 Fun-CosyVoice3-0.5B 与 wetext 到 /data（原路径不变）；
# 音色配置与资产（voices_config.json / data/CosyVoice）也一并烘焙进镜像，
# 无卷挂载即可运行；如需替换音色，挂载宿主机目录到 /data 覆盖即可
FROM registry.cn-shanghai.aliyuncs.com/cyf7950/cuda:12.6.2-cudnn-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    UV_HTTP_TIMEOUT=600 \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"


RUN sed -i 's|deb http://archive.ubuntu.com/ubuntu|deb https://mirrors.aliyun.com/ubuntu|g; s|deb http://security.ubuntu.com/ubuntu|deb https://mirrors.aliyun.com/ubuntu|g' /etc/apt/sources.list \
    && apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates git ffmpeg libsndfile1 libgomp1 build-essential unzip \
    && rm -rf /var/lib/apt/lists/* \
    && curl -LsSf https://astral.sh/uv/install.sh | env UV_INSTALLER_GITHUB_BASE_URL="https://ghfast.top/https://github.com" sh

WORKDIR /app

COPY pyproject.toml uv.lock .python-version ./
RUN uv sync --frozen --no-dev

COPY scripts ./scripts
# 预下载 Fun-CosyVoice3-0.5B 与 wetext 到 /data，与 server.py 加载路径（COSYVOICE_DATA=/data）一致
RUN .venv/bin/python scripts/download_models.py /data

COPY requirements-extra.txt ./
RUN uv pip install --python .venv/bin/python --index-url https://mirrors.aliyun.com/pypi/simple/ "setuptools==69.5.1" wheel \
    && uv pip install --python .venv/bin/python --no-build-isolation --index-url https://mirrors.aliyun.com/pypi/simple/ -r requirements-extra.txt

COPY server.py ./
# 音色配置烘焙进 /data（server.py 按 COSYVOICE_DATA=/data 读取）；无卷挂载运行时也能读到
COPY voices_config.json /data/voices_config.json
COPY data/CosyVoice /data/CosyVoice
# 解压 CosyVoice 源码包并移动为 ./CosyVoice（zip 已随 COPY scripts 进入 /app/scripts）
RUN unzip -q scripts/CosyVoice-main.zip -d /tmp \
    && rm -rf ./CosyVoice \
    && mv /tmp/CosyVoice ./CosyVoice \
    && rm -f scripts/CosyVoice-main.zip

ENV COSYVOICE_DATA=/data \
    COSYVOICE_SRC=/app/CosyVoice \
    MODELSCOPE_CACHE=/data/modelscope \
    TTS_VOICE_ID=zh-male-news \
    MEETING_BOT_KEY=dev-key

EXPOSE 8000
CMD [".venv/bin/python", "-m", "uvicorn", "server:app", "--host", "0.0.0.0", "--port", "8000"]
