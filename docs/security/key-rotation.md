# 密钥轮换手册（Security Key Rotation）

> 背景：早期 `appsettings.json` / `appsettings.Development.json` 曾把默认凭据提交进 git 历史（数据库密码、MinIO `minioadmin/minioadmin`、ABP 字符串加密 passphrase，旧值已脱敏、完整值见 git 历史与内部记录）。2026-08 已从配置文件移除并改为环境变量注入，但 git 历史中仍可检索到旧值。**任何可访问仓库的人均可能掌握旧凭据，生产/共享环境必须按本文档轮换。**

## 1. 受影响资产与当前状态

| 凭据 | 旧值（已泄露） | 当前状态 | 轮换责任人 |
|------|---------------|---------|-----------|
| PostgreSQL 密码 | `postgres` | 本地容器默认（start.ps1），生产未配置 | 部署负责人 |
| MinIO Access/Secret | `minioadmin` / `minioadmin` | 已从配置移除，S3 密钥走环境变量 | 部署负责人 |
| StringEncryption passphrase | `jyl2qty****`（完整值见内部记录） | 已轮换为 .env 中新随机值（见下） | 已处理（本地） |
| Storage 签名密钥 | `dev-only-signing-secret-change-me` | 已轮换为 .env 中新随机值 | 已处理（本地） |
| AnGIneer / LLM API Key | 从未入库 | .env 提供 | 无风险 |
| AI_GATEWAY_API_TOKEN / AI_GATEWAY_INGEST_TOKEN | 从未入库 | 仅 .env，网关与后端进程各持一份 | 见 3.6 |

## 2. 本地（开发机）已完成

- 根目录 `.env`（gitignored）已生成新的 `STRING_ENCRYPTION_PASSPHRASE` 与 `STORAGE_LOCAL_SIGNING_SECRET`（随机 256bit hex），并新增 S3 密钥占位。
- `appsettings.Development.json` 中的硬编码签名密钥已清空，改由 `.env` 提供。
- 本地 DB 密码保持 `postgres`（仅本机开发容器使用，且 .env 不入库）；如本地 DB 在旧 passphrase 下加密过设置项，token 签发可能报解密错误，处理方式见 3.2。

## 3. 生产/共享环境轮换步骤

### 3.1 数据库（PostgreSQL）
1. 生成新密码：`openssl rand -base64 24`
2. `ALTER USER postgres WITH PASSWORD '<new>';`
3. 更新部署环境变量 `BIDCOMPARE_DB_CONNECTION`（Host/DbMigrator 共用同一变量），滚动重启服务。
4. 确认连接串不再出现于任何配置文件/CI 日志。

### 3.2 ABP 字符串加密 passphrase（`STRING_ENCRYPTION_PASSPHRASE`）
1. 生成新值：`openssl rand -hex 32`
2. 更新部署环境变量并重启。
3. **注意**：该密钥用于解密 DB 中以加密存储的设置项（ABP `SettingStore` 中标记 Encrypted 的键）。轮换后旧加密值将无法解密。处理方式：
   - 若这些设置可重新配置 → 直接在管理端重新填写；
   - 若不可 → 先导出明文值，轮换后重新写入；
   - 开发/测试环境可直接清库重跑 `DbMigrator` 种子。
4. 旧 passphrase（`jyl2qty****`，完整值见内部记录）为 ABP 模板默认值，凡是沿用默认值的环境都应视为已泄露。

### 3.3 MinIO / S3（`STORAGE_S3_ACCESSKEY` / `STORAGE_S3_SECRETKEY`）
1. 在 MinIO 控制台生成新的 Access Key/Secret Key（或 `mc admin user add`）。
2. 更新部署环境变量，重启后端。
3. 轮换前已有的对象仍可访问（密钥不参与对象加密），无需迁移数据。

### 3.4 Storage 签名密钥（`STORAGE_LOCAL_SIGNING_SECRET`）
1. 生成新值：`openssl rand -hex 32`。
2. 更新部署环境变量，重启。
3. 轮换会使所有已下发的签名 URL 立即失效（符合预期，前端会重新请求）。

### 3.5 AnGIneer / LLM API Key
- 通过 `ANGINEER_API_KEY`、`LLM_API_KEY`、`LLM_ENDPOINT`、`LLM_MODEL` 注入，从未入库。
- 如怀疑泄露，直接在供应商控制台吊销重建。

> 说明：`LLM_API_KEY` / `LLM_ENDPOINT` / `LLM_MODEL` 已废弃，模型配置统一改为 ai-gateway 的
> `LLM_CONFIGS`（JSON 数组）；`ANGINEER_API_KEY` 继续用于 AnGIneer 文档解析。

### 3.6 网关与服务间令牌（`AI_GATEWAY_API_TOKEN` / `AI_GATEWAY_INGEST_TOKEN`）
1. 生成随机值：`openssl rand -hex 32`。
2. 写入 `.env`（或部署环境变量）：`AI_GATEWAY_API_TOKEN`（ABP→网关出站，`X-API-Key`）与
   `AI_GATEWAY_INGEST_TOKEN`（网关→ABP 用量上报，`X-Gateway-Token`）各一份。
3. 修改后重启 ai-gateway 与后端。
4. 网关与后端之间的令牌不属于用户数据：禁止入库、禁止进日志（日志仅打印「已配置」）。

## 4. （可选）清除 git 历史中的旧凭据

重写历史是**破坏性操作**（所有协作者需重新 clone），仅在确认必要且团队知悉时执行：

```bash
# 1. 安装 git-filter-repo：pip install git-filter-repo
# 2. 对仓库做一次性清理（替换旧值，历史中出现即替换；旧 passphrase 完整值从 git 历史/内部记录获取）
git filter-repo --replace-text <(printf '<旧passphrase>\nminioadmin\nPassword=postgres\n')
# 3. 强制推送（需协调所有协作者）
git push --force --all
```

替代方案（低风险）：接受历史中存在开发默认值，但确保**所有非本地环境凭据均与历史值不同**——即完成第 3 节轮换。推荐先轮换，历史清理作为可选项。

## 5. 防回归检查清单

- [ ] 新凭据只存在于 `.env`（gitignored）或部署密钥管理（Vault/CI Secret）
- [ ] `appsettings*.json` 不包含任何真实凭据（可搜 `Password=`、`SecretKey`、`PassPhrase`）
- [ ] CI 日志/错误上报不打印连接串
- [ ] 代码审查时禁止合入硬编码凭据（可在 lint 阶段加 `secret-scan`）
