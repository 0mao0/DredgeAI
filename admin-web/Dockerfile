# ============================================================
# Dockerfile — admin-web 管理端容器化部署
#
# 本仓库是 pnpm workspace 单体仓库，admin-web 的构建依赖仓库根多处内容：
#   - file:../vendor/angineer-docs-ui（git submodule，源码形态本地包，
#     pnpm 安装时整体物化进 node_modules）
#   - @shared 别名指向 ../packages/shared 源码（vite alias + tsconfig paths）
#   - 根 pnpm-workspace.yaml / pnpm-lock.yaml / tsconfig.base.json
# 因此与单仓项目不同，构建上下文必须是仓库根目录（而非 admin-web 子目录）：
#   docker build -f admin-web/Dockerfile -t dredge-ai-admin-web .
# 前置：vendor 子模块已初始化（git submodule update --init）
#
# 配置注入：admin-web/.env.production 随源码进入构建，构建时打进产物
#（默认同源反代 /api/admin/，由外部网关转发到后端），无运行时占位符替换
#
# Stages:
#   1. builder — 按清单装依赖（缓存层）→ 复制源码 → vite 构建
#   2. runtime — Nginx 官方镜像托管静态产物
# ============================================================

# ===== Stage 1: builder =====
FROM registry.cn-shanghai.aliyuncs.com/cyf7950/node:22.18.0-alpine3.21 AS builder
WORKDIR /repo

# 安装 pnpm（与根 package.json packageManager 对齐）
RUN npm install -g pnpm@9.1.4 --registry=https://registry.npmmirror.com

# 根 package.json 的 prepare 脚本为 husky，容器内无 .git，禁用避免 install 失败
ENV HUSKY=0

# ---- 依赖层：仅清单 / 锁文件 / submodule 变化时重装 ----
# workspace 解析需要全部成员的 package.json（user-web 虽不参与本镜像构建，
# 但缺少清单会导致 install 失败）；vendor 是 file: 本地包，安装时会把目录
# 内容整体物化进 node_modules，必须整目录复制
COPY package.json pnpm-workspace.yaml pnpm-lock.yaml .npmrc ./
COPY admin-web/package.json admin-web/package.json
COPY user-web/package.json user-web/package.json
COPY packages/shared/package.json packages/shared/package.json
COPY vendor ./vendor

RUN pnpm install --frozen-lockfile

# ---- 源码层 ----
# tsconfig extends 链需要根 tsconfig.base.json；@shared 源码经别名直接参与构建
COPY tsconfig.base.json ./
COPY packages ./packages
COPY admin-web ./admin-web

# 构建脚本：vue-tsc -b && vite build（配置来自 admin-web/.env.production）
RUN NODE_OPTIONS="--max-old-space-size=6144" pnpm --filter admin-web build

# ===== Stage 2: runtime =====
FROM registry.cn-shanghai.aliyuncs.com/cyf7950/nginx:1.30.1-alpine3.23-slim
WORKDIR /app

# 复制构建产物
COPY --from=builder /repo/admin-web/dist /app

# 复制 nginx 配置（覆盖默认）
COPY admin-web/deploy/nginx.conf /etc/nginx/nginx.conf

EXPOSE 80

# 不写 ENTRYPOINT 和 CMD —— 沿用官方 nginx 镜像默认值
# 官方 ENTRYPOINT: /docker-entrypoint.sh
# 官方 CMD:        ["nginx", "-g", "daemon off;"]
