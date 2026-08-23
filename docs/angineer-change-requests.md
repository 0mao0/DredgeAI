# Angineer 变更需求（DredgeAI 侧提交）

> 本文档是 DredgeAI 对 Angineer docs-api / docs-ui 的正式变更请求清单。
> 请 Angineer 按以下条目排期，发版后 DredgeAI 更新 submodule / 依赖即可。

## 当前状态（2026 核对 v0.1.6 / 8215da3）

| # | 需求 | 状态 |
|---|---|---|
| 1 | 读标目录/大纲识别不完整 | ❌ 未包含在 v0.1.6（docs-api 侧） |
| 2 | PDF_Viewer 原生 PDF 文本搜索 | ✅ v0.1.6 已实现：未传 `searchText` 时自动逐页用 pdf.js `getTextContent()` 搜索 |
| 3 | PDF_Viewer 标题 prop / slot | ✅ v0.1.6 已实现：`title?: string`，传入后替代“原文”标签显示 |
| 附带 | `.pdf-virtual-spacer` 高度被压缩 | ✅ v0.1.5 已内置 `flex-shrink: 0`，DredgeAI 本地 workaround 已移除 |

## 复核记录

- 2026-08-19 首次核对：`main`（8466126）、`v0.1.5`（228e229）、`master`（4ffc5cc）均无 `title` prop 和 pdf.js 原生搜索；`a5aa7c0` 无法解析。
- 2026-08-22 二次核对：Angineer 重新发布 `v0.1.6`（8215da3），源码确认已包含：
  - `title?: string` prop；
  - `performNativeTextSearch()`：未传 `searchText/textContent` 时逐页用 pdf.js 文本搜索。
- DredgeAI 已更新到 v0.1.6，并移除本地 CSS 标题 hack。

## 二次确认说明（v0.1.5 核对后仍需 Angineer 处理）

### 第 2 条：搜索仍未满足“原生 PDF 文本搜索”

v0.1.5 的 `performTextSearch()` 仍然：

```ts
const sourceText = props.searchText || props.textContent || ''
const lines = sourceText.split('\n')
const pageMap = highlightPageMap.value
```

- 未传 `searchText` / `textContent` 时，**不会回退到 pdf.js 的 `getTextContent()`**；
- 页码映射仍依赖 highlight 的 `lineStart` / `lineEnd`；
- DredgeAI 读标/比标只传 PDF URL + bbox 高亮，没有全文和行号，因此搜索依然无结果。

请按第 2 条实现：`searchText` 为空时，自动用组件内已有的 `loadPageTextItems()` / `pdfPage.getTextContent()` 做全文搜索，并支持结果列表、页码跳转和词级高亮。

### 第 3 条：标题 prop / slot 仍未增加

v0.1.5 的 `PDF_Viewer.vue` props 仍无 `title` / `headerTitle`，`pane-title-main` 仍只有“原文”标签和状态 tag。

DredgeAI 目前用 CSS 注入标题，等待 Angineer 增加正式 `title` prop 或 `#title` slot 后移除 workaround。

---

## 1. 读标目录/大纲识别不完整

- **模块**：angineer-docs-api（解析 structure / outline / graph 阶段）
- **现象**：
  - DredgeAI 读标工作区左侧“目录”没有完整识别；
  - 当前结果第一个节点是“1.6 保密”，缺少 1.1~1.5 等前置章节。
- **原因定位**：
  - 目录来自 Angineer 解析产物中的 structure/outline；
  - DredgeAI 只是消费 `GET /api/v1/documents/{docId}/artifacts` 及 DredgeAI 映射后的 outline，不修改内容。
- **期望**：
  - 目录应覆盖文档完整章节，层级与文档实际标题一致；
  - 不应从中间章节开始，也不应漏掉前置章节。
- **验收标准**：
  - 使用同一个招标文件重新解析后，左侧目录包含完整章节且从文档开头标题/第一章开始。

---

## 2. PDF_Viewer 搜索需要支持原生 PDF 文本搜索

- **模块**：angineer-docs-ui（`PDF_Viewer.vue`）
- **现象**：
  - DredgeAI 读标/比标中，点击 PDF 工具栏搜索按钮，输入关键词无结果。
- **原因定位**：
  - 当前 `performTextSearch()` 强依赖外部传入 `searchText` / `textContent` 全文；
  - 同时依赖 highlight 的 `lineStart` / `lineEnd` 行号映射页码；
  - DredgeAI 集成只传 PDF URL 和高亮 bbox，没有传全文/行号，因此永远搜不到。
- **期望**：
  - 未提供 `searchText` 时，`PDF_Viewer` 自动使用 pdf.js 页面文本内容（组件内已有 `loadPageTextItems()` / `pdfPage.getTextContent()`）做全文搜索；
  - 搜索命中后支持结果列表、页码跳转、词级/行级高亮；
  - 若调用方传入 `searchText`，保留现有逻辑以兼容 Angineer 原 workspace。
- **验收标准**：
  - 上传一份文本型 PDF，搜索任意文档内文字能出结果并跳转/高亮；
  - 原有传入 `searchText` 的调用不受影响。

---

## 3.（可选）PDF_Viewer 增加标题 prop / slot

- **模块**：angineer-docs-ui（`PDF_Viewer.vue`）
- **现象**：
  - DredgeAI 需要在 PDF 左上角显示“招标文件”等文档标题；
  - 当前组件头部只有“原文”标签和状态 tag，没有标题展示位。
- **期望**：
  - 新增 `title?: string` prop（或 `#title` slot）；
  - 传入后在 `pane-title-main` 中显示，未传时保持原行为；
  - 与现有“原文”标签/状态 tag 兼容。
- **验收标准**：
  - 传入 `title` 后左上角显示该标题；
  - 不传 `title` 时界面与旧版一致。
