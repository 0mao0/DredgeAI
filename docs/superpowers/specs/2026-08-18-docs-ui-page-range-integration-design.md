# docs-ui v0.1.2 集成与 PDF page-range 上游化 设计

> 状态：已确认 · 2026-08-18
> 提出方：DredgeAI 团队

## 1. 背景与目标

`@angineer/docs-ui` v0.1.2 已发布"PDF 高亮悬停显示原文"能力：`PDF_Viewer` 内置
浮框，悬停证据高亮框时展示该 bbox 区域原文，命中段加粗，取字复用组件内部按页缓存的
pdfjs，不二次加载 PDF。DredgeAI 消费端（`PdfViewer.vue` wrapper）仍有自研兜底实现，
本轮将其移除并切换到内置能力。

同时，compare 页面的"证据页"视图（`PdfRangeViewer.vue`）是 DredgeAI 本地自绘 canvas
实现，重复加载同一份 PDF。该能力应上游化为 `PDF_Viewer` 的 page-range 模式
（docs-ui v0.1.3），随后 DredgeAI 删除本地实现。

## 2. 现状盘点

- `vendor/angineer-docs-ui` 为 git submodule，锁 commit `4b96e23`（0.1.0）；
  上游 v0.1.2 = commit `7d861a2`（feat: PDF 高亮悬停显示原文）。
- v0.1.2 新增 props：`highlightHoverText`（默认 true）、`highlightHoverFontSize`（13）、
  `highlightHoverMaxWidth`（340）、`highlightHoverMaxHeight`（180）；
  `highlights` 条目新增可选 `text` / `matchText` / `excerpt`（兼容别名）。
- v0.1.2 为破坏性变更：移除 `officePreviewUrl` prop。
- DredgeAI 消费端：
  - `user-web/src/views/ai-bid/compare/components/PdfViewer.vue`：本地兜底
    （二次 pdfjs 加载、DOM 事件委托、自绘 `.pdf-hover-pop` 浮框）。
  - `PdfWorkspace.vue`：双文档工作区，依赖 wrapper 的 `@loaded` 事件重放定位跳页。
  - `PdfRangeViewer.vue`（未跟踪文件）：证据页视图，自绘 canvas，无 hover。
  - `admin-web/src/views/data/static/standards/components/StandardPdfViewer.vue`：
    另一直接消费点，传了 `office-preview-url=""`。
- docs-ui 发布机制：AnGIneer monorepo `packages/docs-ui` 为源，
  `scripts/sync-standalone.ps1` 同步到独立仓库 `0mao0/angineer-docs-ui` 并推送，
  再打 tag。

## 3. 范围

本轮执行：

- Part 1：v0.1.2 集成（两个直接消费点）。
- Part 2：docs-ui 上游开发 page-range + `pdf-loaded`，发布 v0.1.3。

Part 3（v0.1.3 落地后切换证据页视图）依赖 v0.1.3 发布，随发布后另行执行，
不纳入本轮提交。

明确不做：

- aichat-ui 接入。
- 索引树/图联动（原 T2）。
- 给 `PdfRangeViewer` 本地补 hover（避免重复实现）。

## 4. Part 1 — v0.1.2 集成（DredgeAI）

### 4.1 submodule 升级

1. `git -C vendor/angineer-docs-ui checkout v0.1.2`（detached HEAD，锁 `7d861a2`）。
2. `pnpm install`，刷新 lockfile（`file:` 依赖版本 0.1.0 → 0.1.2）。
3. 提交 submodule gitlink 与 lockfile 变更。

### 4.2 user-web `PdfViewer.vue`

删除本地兜底：

- pdfjs 导入与 worker 配置、`pdfDoc` / `pdfLoadingTask` / `pageDataCache`。
- `onHoverOver` / `onHoverOut` / `showBoxPopup` / `buildSegments` / `splitAt` /
  `mapCompactIndex` / `matchHighlight` / `textInRect` / `loadPageData`。
- hover 状态（`hoverSegments` / `hoverVisible` / `hoverPos` / `hoverWidth` / `hoverStyle`）。
- 模板中的 `.pdf-hover-pop` 浮框与对应样式。

其他改动：

- 移除 `office-preview-url=""`。
- highlights 映射补充 `excerpt: h.excerpt`，透传给 docs-ui 作为命中段加粗依据。
- `@loaded` 对外接口保持不变：临时用 MutationObserver 观察 `PDF_Viewer` 根内
  `canvas[data-page]` 首次出现作为加载完成信号，`fileUrl` 变化时重置，卸载时断开。

保留不变：

- `.pdf-virtual-spacer` flex-shrink 修复、`.pane-title-prefix` 隐藏。
- `canPreviewPdf`（doc/docx 转 PDF）、`hideOriginalLabel`、`EmptyState` 分支。

### 4.3 admin-web `StandardPdfViewer.vue`

- 移除 `office-preview-url=""`。
- hover 默认开启即生效；`StandardHighlight` 无 `text`/`excerpt`，走 docs-ui 内部
  按 bbox 取字。

### 4.4 验证

- `pnpm --filter user-web typecheck`、`pnpm --filter admin-web typecheck`、双端 build。
- compare 页手工回归：悬停高亮框 ≤300ms 出原文、命中段加粗、普通文本区不触发、
  明暗主题、翻页/缩放/搜索/定位后无残留浮框、双文档定位重放正常。

## 5. Part 2 — docs-ui 上游 v0.1.3

### 5.1 接口约定

新 prop（向后兼容）：

| prop | 类型 | 默认 | 说明 |
| :--- | :--- | :--- | :--- |
| `pdfPageRange` | `number[]` | `undefined` | 绝对页码数组；undefined 或空数组 = 整篇；越界页码过滤，过滤后为空退化为整篇 |

新事件：

| 事件 | payload | 说明 |
| :--- | :--- | :--- |
| `pdf-loaded` | `source: string` | 文档加载完成后 emit，payload 为当前源 URL |

### 5.2 行为约定

- 虚拟滚动布局只覆盖子集页；`renderedPageRange` / `activePdfPage` 按子集语义计算，
  但页码值一律为绝对页码。
- `current-pdf-page` watcher、`scrollToPdfPage`、`scrollToHighlight`：子集外页码
  就近吸附到最近的子集页（距离相等时取较小页）。
- prev/next 在子集内跳转，子集边界禁用。
- 页码输入框显示绝对页码，分母显示文档总页数。
- 高亮框只渲染在子集页上（由子集页循环自然保证）。
- hover 取字、缩放、搜索全部复用 v0.1.2 已有能力。
- 文档切换时重置 range 相关内部状态。

### 5.3 实现位置

- `src/components/common/viewers/PDF_Viewer.vue`（prop/事件/滚动层）。
- 纯函数放 `src/utils/`（子集过滤、最近子集页吸附），保持无副作用。
- `node:test` 覆盖纯函数；README、CHANGELOG、`package.json` 版本 0.1.3。

### 5.4 发布

在 `D:\AI\AnGIneer` 跑 `scripts/sync-standalone.ps1` 同步独立仓库，然后打 tag
`v0.1.3` 并推送。

## 6. Part 3 — v0.1.3 落地后（DredgeAI 证据页切换）

1. 升 submodule 到 v0.1.3。
2. `PdfViewer.vue` 增加 `page-range?: number[]` prop，透传 `:pdf-page-range`；
   `@loaded` 切换为官方 `@pdf-loaded`，删除 MutationObserver。
3. `PdfWorkspace.vue` 的"证据页"模式改用 `PdfViewer` + `:page-range`
   （传 `leftRange` / `rightRange`）；删除 `PdfRangeViewer.vue`。
4. 24 页上限回退逻辑保留在 `PdfWorkspace`（业务规则，不上游）。
5. 回归：证据页模式悬停、全篇/证据页切换、双文档定位、明暗主题。

## 7. 错误处理与边界

- `pdfPageRange` 为 undefined / 空数组：整篇渲染。
- range 含越界页码：过滤；过滤后为空：退化为整篇渲染，不报错。
- 高亮 page 不在子集：不渲染该框。
- 文档切换：range 相关状态重置；DredgeAI 侧 `PdfWorkspace` 已 watch documents 清空
  旧定位任务。
- MutationObserver 临时方案：`fileUrl` 变化重置标记，卸载时断开，避免泄漏。

## 8. 风险与注意

- DredgeAI 工作区有未提交改动（`PdfWorkspace.vue`、`manifests.ts`、
  `UnifyCompareTable.vue`、`index.vue`、未跟踪的 `PdfRangeViewer.vue`）。
  Part 1 不触碰这些文件；Part 3 只改 `PdfWorkspace.vue` 并删除
  `PdfRangeViewer.vue`，以当前工作区状态为基线，不覆盖其他未提交改动。
- `officePreviewUrl` 移除是破坏性变更：两个消费端都必须清理，否则属性会落到
  `PDF_Viewer` 根元素上。
- v0.1.2 浮框样式走 `--dp-*` CSS 变量与 dark-mode，需要回归明暗主题。
- 上游实现与 DredgeAI 集成存在发布时序依赖：Part 2 完成且 v0.1.3 可用后，Part 3
  才能落地。

## 9. 验收标准

### Part 1

1. `vendor/angineer-docs-ui` 锁 v0.1.2；双端 typecheck + build 通过。
2. compare 页悬停高亮框显示原文并加粗命中段，无重复 PDF 加载请求。
3. 翻页、缩放、搜索、定位后无残留浮框；明暗主题正常。
4. 文档加载中点击证据，加载完成后定位重放不丢。

### Part 2

1. docs-ui 单测（node:test）通过；README/CHANGELOG 更新；版本 0.1.3。
2. `pdfPageRange` 行为符合 §5.2；`pdf-loaded` 在文档加载完成后触发。

### Part 3

1. submodule 锁 v0.1.3；`PdfRangeViewer.vue` 删除。
2. 证据页模式悬停原文可用；24 页上限回退行为不变。
3. 双端 typecheck + build 通过，回归清单通过。
