# 第三方库改动交接（Angineer）

## @angineer/docs-ui：PDF 溯源 bbox 居中

状态：✅ 已合并并随 **v0.1.7** 发版；`vendor/angineer-docs-ui` submodule 已升级到
`ffd695c`（v0.1.7），应用侧包装层 `user-web/src/views/ai-bid/compare/components/PdfViewer.vue`
已默认开启 `center-active-highlight`（读标/比标溯源均生效）。
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
