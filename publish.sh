#!/usr/bin/env bash
# 生成 Docker Compose 部署文件
# 用法: ./publish.sh [类别] [输出根目录名] [--no-dashboard]
# 类别: frontend | python | dotnet | all（默认 all）
# --no-dashboard: 发布产物不含 docker-compose-dashboard
# 默认输出目录: ./aspire-output/<类别>（仓库根目录下）

set -euo pipefail

# === 配置 ===
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
APPHOST_DIR="${SCRIPT_DIR}/backend/DredgeAI.AppHost"
CATEGORY="${1:-all}"
OUTPUT_ROOT_NAME="${2:-aspire-output}"
OUTPUT_DIR="${SCRIPT_DIR}/${OUTPUT_ROOT_NAME}/${CATEGORY}"
WITH_DASHBOARD=true
for arg in "$@"; do
  [[ "${arg}" == "--no-dashboard" ]] && WITH_DASHBOARD=false
done

# 避免 sandbox 无法写入 ~/.aspire 导致命令挂起
export ASPIRE_LOG_PATH="${ASPIRE_LOG_PATH:-/tmp/aspire-logs}"
mkdir -p "${ASPIRE_LOG_PATH}"

# === 前置检查 ===
case "${CATEGORY}" in
  frontend|python|all) TIER="${CATEGORY}" ;;
  dotnet)              TIER="backend" ;;
  *) echo "错误: 非法类别 '${CATEGORY}'，合法值: frontend | python | dotnet | all" >&2; exit 1 ;;
esac

if ! command -v dotnet >/dev/null 2>&1; then
  echo "错误: 未找到 dotnet 命令，请先安装 .NET SDK" >&2
  exit 1
fi

# 检查 aspire 工具是否可用
if ! command -v aspire >/dev/null 2>&1; then
  echo "错误: 未找到 aspire 命令，请安装 Aspire 工作负载: dotnet workload update" >&2
  exit 1
fi

if [[ ! -d "${APPHOST_DIR}" ]]; then
  echo "错误: 未找到 AppHost 目录: ${APPHOST_DIR}" >&2
  exit 1
fi

cd "${APPHOST_DIR}"

echo "=========================================="
echo " Aspire Publish"
echo " 发布类别: ${CATEGORY} (tier=${TIER})"
if [[ "${WITH_DASHBOARD}" == "true" ]]; then echo " Dashboard: 包含"; else echo " Dashboard: 不含"; fi
echo " 项目目录: ${APPHOST_DIR}"
echo " 输出目录: ${OUTPUT_DIR}"
echo " 日志目录: ${ASPIRE_LOG_PATH}"
echo "=========================================="

# === 清理旧输出 ===
if [[ -d "${OUTPUT_DIR}" ]]; then
  echo "清理旧输出目录: ${OUTPUT_DIR}"
  rm -rf "${OUTPUT_DIR}"
fi

# === 执行发布 ===
echo "开始执行 aspire publish..."
PUBLISH_ARGS=(publish --output-path "${OUTPUT_DIR}" -- --tier="${TIER}")
[[ "${WITH_DASHBOARD}" == "false" ]] && PUBLISH_ARGS+=(--dashboard=false)
aspire "${PUBLISH_ARGS[@]}"

# === 结果校验 ===
COMPOSE_FILE="${OUTPUT_DIR}/docker-compose.yaml"
ENV_FILE="${OUTPUT_DIR}/.env"

if [[ ! -f "${COMPOSE_FILE}" ]]; then
  echo "错误: 生成失败，未找到 ${COMPOSE_FILE}" >&2
  exit 1
fi

echo ""
echo "=========================================="
echo " 发布成功"
echo "=========================================="
echo " docker-compose: ${COMPOSE_FILE}"
echo " .env 模板:      ${ENV_FILE}"
echo ""
echo "下一步:"
echo "  1. 编辑 ${ENV_FILE} 填入实际参数值"
echo "  2. docker compose -f ${COMPOSE_FILE} --env-file ${ENV_FILE} up -d"
echo "=========================================="
