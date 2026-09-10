# 第三方库改动交接（Angineer）

## @angineer/docs-ui：PDF 溯源 bbox 居中

状态：✅ 已合并；`vendor/angineer-docs-ui` submodule 当前钉在 `da869ac`（**v0.2.2**），
应用侧包装层 `user-web/src/views/ai-bid/compare/components/PdfViewer.vue`
已默认开启 `center-active-highlight`（读标/比标溯源均生效）。
该能力随 v0.1.7 交付，v0.1.8 / v0.1.9 又统一收敛了引用定位与居中逻辑，v0.2.x 行为不变。
下方补丁与验收标准作为交接记录保留，无需再次应用。

### 问题

DredgeAI 读标/比标页点「溯源」时，父组件通过 `currentPdfPage` + `highlights` +
`activeHighlightId` 程序化定位。`PDF_Viewer` 目前只对 `currentPdfPage` 变化执行
`scrollToPdfPage`（页顶对齐），bbox 可能落在视口外，用户需要手动滚动查找。
组件内已有 `scrollToHighlight(highlight, 'center')`（纵向居中），但只接在全文搜索跳转上，
没有接在「外部切换 activeHighlightId」上。

### 期望行为

外部切换 `activeHighlightId` 时，对应高亮 bbox 纵向居中于 PDF 视口（无需手动滚动）。

### 补丁内容（对 `src/components/common/viewers/PDF_Viewer.vue`）

1. 新增可选 prop `centerActiveHighlight?: boolean`，默认 `false`（不改变既有行为）；
2. 新增 watcher：监听 `[centerActiveHighlight, activeHighlightId, highlights]`，
   开启且存在匹配高亮时调用 `scroll.scrollToHighlight(active, 'center')`；
3. 按 `itemId|page|top` 去重，同一高亮重复激活不重复滚动；
4. 目标页未渲染/未测量时复用现有 `waitForPageMeasured` 两步定位。

### 验收标准

- 传入 `centerActiveHighlight=true`：切换 `activeHighlightId` 后 bbox 在视口内纵向居中；
- 不传该 prop：行为与现状完全一致；
- bbox 所在页尚未渲染：先跳页，渲染测量完成后仍能最终居中；
- 同一高亮重复激活不产生额外滚动。

### DredgeAI 侧（发版后）

1. 升级 `vendor/angineer-docs-ui` submodule 到含该功能的版本；
2. 在 `user-web/src/views/ai-bid/compare/components/PdfViewer.vue` 包装层透传
   `center-active-highlight`，并在读标/比标溯源场景传入 `true`。

## docs-ui 0.2.x 升级须知（v0.1.7 → v0.2.2）

本轮升级实际踩到的坑，按"下次还会遇到"的顺序记录：

1. **新增 registry 运行时依赖 `@angineer/smartree@^0.1.1`**。v0.2.0 把本地
   `SmartTree.vue`（1101 行）外置为独立包，并写进 docs-ui 的 `dependencies`。
   `@angineer/docs-ui` 是 `file:` 本地包，pnpm 会连它的 `dependencies` 一起装，
   因此**安装环境必须能访问 npm registry**（npmjs 与 npmmirror 均有 0.1.1 / 0.1.2）。
   消费方代码无需改动：`PDF_Viewer` / `PDFParsedViewerCombo` / `PreviewMode` /
   `StructuredIndexItem` 的入口与导出在 0.2.x 全部保留；user-web、admin-web、
   packages/shared 均未使用 SmartTree。

2. **`pnpm install` 不会重新解析 `file:` 目录依赖**。升级 submodule 之后，
   `pnpm install` 与 `pnpm install --force` 都会报 "Already up to date"：
   lockfile 不更新、新依赖不装（Docker 里的 `--frozen-lockfile` 会因此装出残缺依赖，
   构建时才以 "Failed to resolve import" 报出来）。实测可行的方法是指名重装：
   `pnpm add "@angineer/docs-ui@file:../vendor/angineer-docs-ui"`——跑一次即可，
   lockfile 的 resolution 是全局的，另两个 importer 会同步生效。

3. **smartree 只发源码**（`main` / `types` 指向 `./src/index.ts`，含 1 个 `.vue`、
   1 个 `.less`），需由消费方 Vite 转译。实测 user-web / admin-web 的
   `vite build` 与 `vite optimize` 均通过，且 smartree 不会进 esbuild 预打包列表
   （走源码管线处理），**无需额外的 `optimizeDeps` / `resolve.alias` 配置**。

4. **smartree 的 README 与事实不符**：它写着"未发布到 npm，请用 github tag 安装"，
   而 npm 上确实有 0.1.1 / 0.1.2。不要照它的 README 把依赖改成 git URL。

5. **BOM 史**：`package.json` 带 UTF-8 BOM 的版本是 v0.1.6 ~ v0.1.9 与 v0.2.1，
   装不上（pnpm ≥10 严格 `JSON.parse` 直接报 `Unexpected token ''`）。
   v0.2.2 已修复（上游引入过两次、手工修过两次，现 `origin/main` 已有 `ci.yml` 兜底），
   所以**升级时选 v0.2.2 及以后，不要再钉 v0.1.x / v0.2.1**。
