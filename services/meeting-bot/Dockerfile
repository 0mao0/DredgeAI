# AI 晨会模型服务镜像（FireRedASR / FireRedTTS / InsightFace / YOLO）
# 模型权重不烧入镜像：运行时挂载 D:/AI/AImodles/models -> /app/models
# 构建前置：third_party/FireRedTTS 已在本地（deploy-meeting-bot.ps1 第 3 步）
FROM nvidia/cuda:12.6.2-cudnn-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive \
    UV_LINK_MODE=copy \
    PATH="/root/.local/bin:/app/.venv/bin:$PATH"

RUN apt-get update && apt-get install -y --no-install-recommends \
        curl ca-certificates git ffmpeg libgl1 libglib2.0-0 libgomp1 \
    && rm -rf /var/lib/apt/lists/* \
    && curl -LsSf https://astral.sh/uv/install.sh | sh

WORKDIR /app

# 主环境依赖：torch 2.9.1(cu126)、fireredasr、insightface、ultralytics 等
COPY pyproject.toml uv.lock ./
RUN uv sync --group models --no-dev

# 应用代码 + FireRedTTS 源码（本地已重组并打补丁）
COPY app ./app
COPY third_party ./third_party

# FireRedTTS 专用 Python 3.10 环境（镜像内双解释器，worker 常驻子进程）
RUN uv venv --python 3.10 .venv-tts \
    && uv pip install --python .venv-tts torch==2.3.1 torchaudio==2.3.1 \
        --index-url https://download.pytorch.org/whl/cu121 \
    && uv pip install --python .venv-tts -e third_party/FireRedTTS \
    && uv pip install --python .venv-tts \
        "diffusers==0.27.2" "librosa==0.10.2" "soundfile==0.12.1" "einops==0.8.0" \
        "transformers==4.44.2" "tiktoken==0.7.0" "inflect==7.4.0" \
        "lingua-language-detector==2.0.2" "sentencex==0.6.1" \
        "huggingface-hub==0.25.2" "numpy<2"

ENV MODEL_DIR=/app/models \
    TTS_VENV_PYTHON=/app/.venv-tts/bin/python

EXPOSE 8101
CMD [".venv/bin/python", "-m", "uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8101"]
