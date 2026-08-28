#!/usr/bin/env bash
# 下载 YOLOv8n 权重到 MODEL_DIR（默认 /app/models），与 app/engines/yolo_engine.py 加载路径一致
# 已存在则跳过；可用 MODEL_DIR / YOLOV8N_URL 环境变量覆盖

set -euo pipefail

MODEL_DIR="${MODEL_DIR:-/app/models}"
YOLOV8N_URL="${YOLOV8N_URL:-https://github.com/ultralytics/assets/releases/download/v8.3.0/yolov8n.pt}"

mkdir -p "$MODEL_DIR"
TARGET="$MODEL_DIR/yolov8n.pt"

if [ -f "$TARGET" ]; then
  echo "[download_models] 已存在 $TARGET，跳过下载"
else
  echo "[download_models] 下载 yolov8n.pt -> $TARGET"
  curl -fL --retry 3 --retry-delay 5 -o "$TARGET" "$YOLOV8N_URL"
fi

echo "[download_models] 完成"
