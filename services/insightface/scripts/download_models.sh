#!/usr/bin/env bash
# 下载 InsightFace buffalo_l 模型到 MODEL_DIR/buffalo_l（默认 /app/models/buffalo_l），
# 与 app/engines/insightface_engine.py 加载路径一致（insightface 约定 <root>/models/<name>）
# 已存在则跳过；可用 MODEL_DIR / BUFFALO_L_URL 环境变量覆盖
# 国内构建环境优先走 ghfast.top 代理，失败回退 GitHub 直连

set -euo pipefail

MODEL_DIR="${MODEL_DIR:-/app/models}"
BUFFALO_L_URL="${BUFFALO_L_URL:-}"
GITHUB_URL="https://github.com/deepinsight/insightface/releases/download/v0.7/buffalo_l.zip"

TARGET_DIR="$MODEL_DIR/buffalo_l"
TARGET_ONNX="$TARGET_DIR/w600k_r50.onnx"

mkdir -p "$TARGET_DIR"

if [ -f "$TARGET_ONNX" ]; then
  echo "[download_models] 已存在 $TARGET_ONNX，跳过下载"
  exit 0
fi

TMP_ZIP="$(mktemp)"
trap 'rm -f "$TMP_ZIP"' EXIT

URLS=()
if [ -n "$BUFFALO_L_URL" ]; then
  URLS+=("$BUFFALO_L_URL")
fi
URLS+=("https://ghfast.top/${GITHUB_URL}" "$GITHUB_URL")

for url in "${URLS[@]}"; do
  echo "[download_models] 下载 buffalo_l.zip: $url"
  if curl -fL --retry 3 --retry-delay 5 --connect-timeout 30 -o "$TMP_ZIP" "$url"; then
    echo "[download_models] 解压到 $TARGET_DIR"
    # zip 内为平铺的 onnx 文件（det_10g / w600k_r50 / 1k3d68 / 2d106det / genderage）
    unzip -o -j "$TMP_ZIP" -d "$TARGET_DIR"
    ls -lh "$TARGET_DIR"
    echo "[download_models] 完成"
    exit 0
  fi
  echo "[download_models] 下载失败: $url，尝试下一个源"
done

echo "[download_models] 所有下载源均失败" >&2
exit 1
