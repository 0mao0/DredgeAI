# DredgeAI 接入 angineer-docs-ui 需求（修订版）

> 状态：**主体已实施**，剩余收尾工作 · 版本：v3 · 2026-08-14
> 组件库来源：[angineer-docs-ui](https://github.com/0mao0/angineer-docs-ui) / [angineer-aichat-ui](https://github.com/0mao0/angineer-aichat-ui)
> 本地参照源码：`D:/AI/AnGIneer`（monorepo，含 aichat-ui / docs-ui / sop-ui 等包）
> **范围调整（v3）**：aichat-ui 接入**移出本次范围**（见 §3），本次聚焦 docs-ui 接入收尾。

## 修订说明（v2 → v3）

1. **aichat-ui 移出本次范围**：DredgeAI 现阶段无知识库/问答后端；PRD 明确定位为业务化平台而非聊天工具（"解决首页像聊天工具、业务入口混乱的问题"、"避免传统聊天工具式侧边历史栏"）。aichat-ui 的全套能力（引用卡片/思考步骤/多会话/RAG 字段）依赖知识库型架构，前置条件不满足。v2 中相关任务（T1/T3/T4/T5）、主题映射与决策项全部降级为 §3「后续候选计划」，本次不做；
2. **任务清单收敛为 docs-ui 收尾 3 项**（锁版本 / 可选索引树联动 / 全量回归），合计 0.5–3 人天；
3. v2 已完成的事实核查（核对表、导出面清单、数据能力现状）全部保留。

## 1. 背景与目标（v3 范围）

**本次范围**：复用 docs-ui 的文档解析产物展示能力（PDF 渲染 + 归一化 bbox 高亮），服务比标 compare 模块——已落地大半，本次只做收尾。

**后续候选**：aichat-ui 聊天能力升级（§3），本次不做。

**现状**：compare 页 `PdfViewer.vue` 已用 `PDF_Viewer` 渲染标书并高亮证据（`PdfWorkspace.vue` 工作区）；两处聊天面板（标书追问 / 标准问答）为占位假回复，本次维持不动。

**非目标**：登录/权限体系、限流、API Key 管理、AnGIneer 仓库改造、**aichat 聊天升级（本次）**。

## 2. 依赖接入（本次 = 收尾）

### 2.1 现状核对表（2026-08-14 实测）

| 检查项 | 状态 | 证据 |
|---|---|---|
| docs-ui 依赖 | ✅ 已接入 user-web | `user-web/package.json`：`"@angineer/docs-ui": "https://codeload.github.com/0mao0/angineer-docs-ui/tar.gz/refs/heads/main"` |
| pnpm 安装 + 锁文件 | ✅ | `pnpm-lock.yaml` 已含 `@angineer/docs-ui` 解析记录；pnpm store 有安装产物 |
| Vite 编译 node_modules 内 .vue/.ts/.less 源码 | ✅ 已实证 | docs-ui `main` 直指 `src/index.ts`，`vue-tsc --noEmit` 与 Vite 均通过（下） |
| `optimizeDeps.exclude` | ✅ 已配置 | `user-web/vite.config.ts`：`exclude: ['@angineer/docs-ui']` |
| pdf.js 运行资源（cmaps/fonts/wasm） | ✅ 已实现 | `user-web/vite.config.ts` 的 `pdfWasmPlugin`（docs-ui 的 `vite-pdf-wasm` 子路径未在包 exports 中暴露，此处为等价内联实现，注释已说明） |
| 双端类型检查 | ✅ | 2026-08-14 实测 `pnpm --filter user-web typecheck` 与 `pnpm --filter admin-web typecheck` 均通过 |
| aichat-ui 依赖 | ⏸️ 本次不加 | 待 §3 前置条件满足后启用 |
| 版本锁定 | ⚠️ 漂移风险 | 当前锁 `refs/heads/main`（tar.gz），AnGIneer 更新即漂移 → T1 |

### 2.2 剩余动作

1. **锁版本（唯一必做项）**：本地 monorepo 已有 tag `v0.1-frontend-cleanup` / `v0.1-frontend-ux-fix`；独立仓库 tag 需与 AnGIneer 侧确认，确认前可先锁 commit SHA；改 `user-web/package.json` 后 `pnpm install` 复验；
2. 网络：本机已成功从 `codeload.github.com` 安装（store 有产物）；DredgeAI 私有云 CI/构建机需确认可达 GitHub（公开库无需认证）。

**验收**：`pnpm install` 成功；双端 `vue-tsc --noEmit` + `vite build` 通过；compare 页 PDF 预览与高亮正常。

## 3. aichat-ui：本次为什么不做 + 何时再做（v3 新）

### 3.1 暂缓依据

- **产品定位**：PRD（`docs/prd-ai-platform-prototype.md`）明确"解决首页像聊天工具、业务入口混乱"、"避免传统聊天工具式侧边历史栏"——聊天不是 DredgeAI 的产品形态，只是业务页内的辅助交互；
- **本迭代无此需求**：compare 前端实施计划（`docs/superpowers/plans/2026-07-29-ai-bid-compare-frontend.md`）零处提到聊天；现有"AI 对话"Tab 为占位假回复，未要求激活；
- **能力空转**：aichat-ui 的核心价值（引用卡片、思考步骤、`retrieved_items`、`gap_analysis`）依赖知识库检索架构；DredgeAI 后端无知识库、无问答端点、`/apikey/models` 仅前端 mock。现在接入等于纯 UI 换壳，且需新建统一问答端点（约 2–3 人天后端 + 1–2 人天前端）；
- **位置**：两处聊天使用点（`BidReviewPanel.vue`、`StandardProperty.vue`）均为 `setTimeout` 假回复，本次保持不动、不引入依赖。

### 3.2 何时再做（前置条件，全部满足后再立项）

1. 产品立项"标书追问 / 标准自然语言查询"等对话型能力；
2. 后端知识库（或 compare 任务 IR 作为引用源的轻量 RAG）立项；
3. 届时按 v2 §3 路线 A 实施：统一问答端点 + 网关 SSE 流式 + `AIChatTransport` + 共享 `AIChat.vue` 兼容层（使用点仅 2 个，接口已固定，兼容层方案届时直接复用）。

### 3.3 最小替代（若产品提前要求"追问区"）

不必引入 aichat-ui 全套：现有 `packages/shared/src/web/components/AIChat.vue` + 后端一个流式端点即可满足"追问"场景。引用卡片/思考步骤等增强能力等知识库就绪后再上 aichat-ui。

> 备查：aichat-ui 契约（`AIChatTransport`、`QueryRequest/QueryResponse`）、16 个测试用例、`--chat-*` 主题变量映射方案见 v2 版本历史或 `D:/AI/AnGIneer/packages/aichat-ui/src`，未来启用时直接复用。

## 4. docs-ui 接入（本次主体）

### 4.1 导出面（以已安装包 `exports` 为准，已核查）

包 `exports` 只放行 `.` 与 `./style`，**深路径 import 会被拦截**。

**入口可用的组件**：`PDFParsedWorkspace` / `PDFParsedViewerCombo`（解析产物预览编排）、`PDF_Viewer`（PDF 渲染 + 归一化 bbox 高亮，**已在用**）、`Preview_Markdown`、`SmartTree` / `KnowledgeTree`（资源树/知识树）。

**入口可用的 composables**：`useParsedPdfViewer` / `useParsedPdfIndexTree` / `useKnowledgeTree` / `useKnowledgeParse` / `useKnowledgeStructuredIndex` / `useDocBlocksGraph` / `useWorkspaceLinkage` / `useRefAnchor` / `useWorkspacePreview` / `useKnowledgeCitation` / `useResourceAdapter`。

**存在但未导出**（需 AnGIneer 补导出才能用）：`Preview_IndexTree` / `Preview_IndexGraph` / `Preview_KnowledgeGraph` / `OfficePreview`。

（`SOPTree` 属 `@angineer/sop-ui`，不在 docs-ui。）

### 4.2 DredgeAI 数据能力现状

| docs-ui 需要的数据能力 | DredgeAI 现状 |
|---|---|
| 文档/解析任务接口 | 🟡 compare 任务有 `POST /api/compare/tasks/{id}/documents` 上传 + `/parse` 批量解析；无独立知识库后端 |
| 结构化索引与图谱数据 | 🟡 `GET /api/compare/tasks/{id}/ir/{docId}` 返回 IR blocks（含 bbox），可作索引树/图数据源 |
| 预览文件 URL | ✅ `GET /api/compare/tasks/{id}/documents/{docId}/file`（已开 `EnableRangeProcessing`，pdf.js 流式可用）；`PDF_Viewer` 直接传 `file-url` |
| 引用接口 `/api/docs/references/{blockId}` | ❌ 无；不启用 `useRefAnchor` 即规避 |

### 4.3 已落地与本次收尾范围

**已落地（最小验证已通过）**：compare 页 `PdfViewer.vue` 包装 `PDF_Viewer`（`file-url` + 归一化 bbox 高亮 + `theme` 联动 `useThemeStore.effectiveTheme`）+ `PdfWorkspace.vue` 工作区，双端构建与类型检查通过。

**本次可选项**：基于 IR 的索引树/图联动（`useParsedPdfIndexTree` / `useDocBlocksGraph`）——先推 AnGIneer 补导出（§4.1），或暂用自建轻量索引树；不做知识库上传/解析链路。

**验收**：compare 任务内文档解析 → PDF 预览 → IR 高亮联动可跑通（已在用）；明暗主题无冲突（T3 回归）。

## 5. 主题与样式（本次范围 = docs-ui）

- **docs-ui**：样式入口硬编码 `@text-color: rgba(0,0,0,.85)`、`@primary-color: #1890ff`（非 DredgeAI 品牌 `#0EA5E9`），按需覆盖；`PDF_Viewer` 自带 `theme='light'|'dark'|'auto'` prop 或 `--dp-pane-bg` / `--dp-title-bg` 变量，DredgeAI 已走 `theme` prop 方案（传 `effectiveTheme`）；
- antd 组件经 `useTheme` 的 ConfigProvider token 桥接自动跟随主题，无需额外处理；
- aichat-ui 的 `--chat-*` 变量映射（含 `--bg-primary` 缺口）随 §3 一并延后；
- **明暗主题切换回归**列入 T3。

## 6. 技术风险（本次）

1. ~~源码包 + Vite 编译~~ → **已实证可行**；
2. **docs-ui 导出面限制**：索引树/图等深路径组件被 `exports` 拦截，需上游补导出（提 AnGIneer）；
3. **版本漂移**：当前锁 `main` 需切 tag/commit（T1）；
4. **主题硬编码**：docs-ui 样式默认色与 DredgeAI 品牌/暗色不一致，已局部适配，T3 回归兜底；
5. **网络可达性**：本机已验证；CI/私有云构建机需确认 GitHub 可达。

## 7. 建议实施顺序（v3：本次任务清单）

- [x] **D1 依赖接入（docs-ui）** — 已完成（§2.1）
- [x] **D2 PDF_Viewer 集成 + pdf-wasm + 主题** — 已完成（§4.3）
- [ ] **T1 锁版本 + 构建复验**（约 0.5 天）
  - Files：`user-web/package.json`（docs-ui 依赖从 `refs/heads/main` 切到 tag/commit）
  - 验证：`pnpm install`；`pnpm --filter user-web typecheck`；`pnpm build:user`
- [ ] **T2 索引树/图联动（可选）**（约 1–2 天，依赖 AnGIneer 补导出）
  - Files：compare 页新增索引树/图面板，数据源 `GET /api/compare/tasks/{id}/ir/{docId}`
  - 验证：树/图与 PDF 高亮联动；`pnpm --filter user-web typecheck`
- [ ] **T3 全量回归**（约 0.5 天）：双端构建、compare 文档预览、IR 高亮、明暗主题（含 reduced-motion 降级）

合计剩余 **0.5–3 人天**（T2 可选、不计关键路径）。

## 8. 决策建议（v3，附依据）

| 决策点 | 建议 | 依据 |
|---|---|---|
| aichat 接入时点 | **本次不做**；待"追问/查询"产品立项 + 知识库/问答后端就绪后再按 v2 路线 A 启动 | PRD 反聊天工具定位；compare 计划无聊天需求；后端无问答端点、无知识库，能力会空转 |
| 若产品提前要"追问区" | 现有 AIChat.vue + 后端流式端点即可，**不引 aichat-ui 全套** | §3.3 |
| 版本策略 | 尽快从 `main` 切到 tag/commit | 漂移风险已实际存在 |
| docs-ui 索引树/图 | 可选；先推 AnGIneer 补导出 | 组件有源码但被 `exports` 拦截 |
| 主题融合 | theme prop + 局部覆盖 + 回归，不 fork 组件 | 已按此落地 |

## 9. 参考（全部为已核实路径）

- docs-ui 出口（已安装包）：`node_modules/@angineer/docs-ui/src/index.ts`、`src/components/index.ts`、`src/composables/index.ts`、`src/styles/variables.less`
- DredgeAI 已接入点：`user-web/vite.config.ts`、`user-web/src/views/ai-bid/compare/components/PdfViewer.vue`、`PdfWorkspace.vue`
- DredgeAI 产品定位：`docs/prd-ai-platform-prototype.md`（首页去聊天工具化、审标页追问区）、`docs/superpowers/plans/2026-07-29-ai-bid-compare-frontend.md`
- 后端数据端点：`...HttpApi/Controllers/CompareTaskController.cs`（上传/解析/文件/IR）
- aichat-ui 契约（后续启用备查）：`D:/AI/AnGIneer/packages/aichat-ui/src/api/types.ts`、`src/types/chat.ts`、`src/composables/useAIChat.ts`；测试 16 例：`pnpm dlx tsx --test test/*.test.ts`
- 后端接口规范：`.opencode/rules/abp-api-conventions.md`
