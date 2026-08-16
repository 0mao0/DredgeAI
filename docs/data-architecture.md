# DredgeAI 数据架构（monorepo 根级 data/）

所有运行时数据统一存放在仓库根目录的 `data/` 下，与源码完全隔离。
该目录整体被 `.gitignore` 忽略，不随仓库提交；备份时只需覆盖这一个目录。

## 目录结构

```text
D:\AI\DredgeAI\
├─ data/                        # 运行时数据总目录（不入库）
│  ├─ base/                     # 基础数据：共享参考资料、标准规范、字典、模板等
│  ├─ storage/                  # 业务文件存储（后端 IFileStorage 根目录）
│  │  ├─ compare/               # ai-bid 比标：任务文档 / IR / raw 产物 / 导出
│  │  │  └─ drafts/             # 比标草稿箱文件
│  │  └─ <module>/              # 后续 AI 业务模块按 key 前缀扩展（dubbing、standards…）
│  ├─ postgres/                 # PostgreSQL 数据目录（Docker bind mount）
│  ├─ logs/                     # 各服务日志与 PID 文件（start.ps1 写入）
│  └─ backup/                   # 运维备份（pg_dump 等）
├─ backend/                     # 纯源码
├─ services/                    # 纯源码
├─ admin-web/ user-web/ packages/   # 纯源码
└─ docs/
```

## 分层约定

### 基础数据 `data/base/`

存放跨模块共享、低频更新、可作为输入的静态数据：

- 标准规范库（PDF / 解析产物）
- 公共字典、术语表、模板文件
- 各 AI 模块共用的参考资料

约定：只放“读多写少”的共享数据；由脚本或管理后台维护，不允许业务代码随意写入。

### 业务文件 `data/storage/`

后端 `IFileStorage` 的存储根目录。业务模块必须以 key 前缀隔离，禁止裸文件散落根下：

| 前缀 | 模块 |
|---|---|
| `compare/` | ai-bid 比标（含 `compare/drafts/` 草稿） |
| `dubbing/`、`standards/`、`<module>/` | 后续 AI 应用模块按此扩展 |

单个比标任务的目录形态：

```text
data/storage/compare/{taskId}/{docId}/
├─ origin.pdf / origin.docx     上传原始文件
├─ ir.json                      内部适配 IR
├─ raw/                         AnGIneer 原始产物留档
├─ content.md                   阅读流 Markdown
└─ images/                      文档内截图
data/storage/compare/{taskId}/exports/{jobId}.{ext}   导出报告
```

存储根路径解析规则：宿主启动时读取 `.env` 所在目录，默认映射为
`<repoRoot>/data/storage`；可用环境变量 `STORAGE_LOCAL_ROOT` 显式覆盖。
生产环境通过 `Storage:Provider=S3` 切换到 MinIO/S3（桶 `bid-compare`），
key 约定不变。

### PostgreSQL `data/postgres/`

本地开发使用 Docker 容器 `bidcompare-postgres`（postgres:16），
数据目录 bind mount 到 `data/postgres/`，随 `start.ps1` 自动创建。
备份推荐 `pg_dump`，导出文件放 `data/backup/`。

### 日志 `data/logs/`

`start.ps1` 把各服务日志与 PID 文件写入 `data/logs/`：

- `backend.log` / `backend.err.log`（ABP 后端，Serilog 另写 `logs.txt`）
- `compare-algo.log`
- `frontend.log` / `admin-web.log`
- `*.pid`（进程句柄，供重启清理）

## 备份与恢复

1. 业务文件 + 基础数据：直接备份 `data/storage`、`data/base`。
2. PostgreSQL：`docker exec bidcompare-postgres pg_dump -U postgres -d BidCompare -F c -f /tmp/bid.sql`，
   再把 `/tmp/bid.sql` 拷到 `data/backup/`。
3. 日志无需备份（可清理）。

## 迁移记录

- 2026-08-16：本地文件存储从
  `backend/.../HttpApi.Host/App_Data/storage` 迁至 `data/storage`；
  日志从根 `Logs/` 迁至 `data/logs`；
  PostgreSQL 数据从 Docker 匿名卷迁至 `data/postgres`（bind mount），
  迁移前已 `pg_dump` 备份到 `data/backup/bidcompare-20260816.sql`。
