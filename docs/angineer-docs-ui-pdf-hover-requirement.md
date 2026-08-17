# docs-ui PDF 高亮锚点悬停显示原文 需求说明

> 提出方：DredgeAI 团队
> 目标：在 `@angineer/docs-ui` 的 `PDF_Viewer` 内，为“证据高亮 bbox”提供悬停显示原文的能力。仅在高亮锚点上触发，不是全页面文字 hover。

## 1. 背景与场景

DredgeAI 比标分析页左侧用 `PDF_Viewer` 预览标书，右侧是分析结果（串标查重、条款未响应、指标比选等证据）。交互流程：

1. 用户点击右侧某条证据（如“雷同”）；
2. 左侧两个 PDF 同时对该证据的 bbox 高亮（`PDF_Viewer` 的 highlights 能力，当前已支持）；
3. 由于 PDF 缩放较小、字看不清，用户把鼠标悬停到**高亮框**上时，希望立刻弹出浮框，显示该处原文，让人能读清。

注意：**不需要对普通文本区域做 hover**，只在用户已点击证据后产生的高亮锚点上触发。

## 2. 现状与问题

DredgeAI 当前在消费侧（`PdfViewer.vue` wrapper）兜底实现：

- 通过 DOM 事件委托识别 `.pdf-highlight-box`，用几何最近匹配回 `props.highlights`，优先显示高亮数据里的原文片段（excerpt）；
- excerpt 缺失时，用 pdf.js **单独再加载一次 PDF**，按 bbox 区域提取文字兜底。

局限：

1. 同一份 PDF 被加载两次（多一次全量/范围请求）；
2. 高亮框与 `props.highlights` 的匹配是“几何最近”近似，多锚点重叠时可能取错；
3. 能力散落在消费方，读标/清标等其他模块无法复用；
4. 消费方拿不到 `PDF_Viewer` 内部已加载的 pdfjs 文档与缩放，无法精确按 bbox 取字。

## 3. 需求

### 3.1 功能行为

- 用户悬停在**证据高亮框**（`.pdf-highlight-box`）上时，快速（建议 ≤100ms）显示浮框，展示该锚点的原文；
- 浮框展示的是**该 bbox 区域内的全部内容**，其中“雷同/命中的那段文字”需要加粗或高亮（如红字加粗 + 浅红底色），让人一眼看到匹配位置；
- 鼠标离开高亮框或预览区时浮框消失，不残留；
- 悬停普通文本区域不触发浮框；
- 浮框字号可读（建议默认 13px，可通过 prop 调整）；亮色/暗色主题正常；
- 浮框 `pointer-events: none`，不遮挡后续交互；靠近右/下边缘时自动翻转防溢出；
- 翻页、缩放、搜索、定位等操作不产生残留浮框或卡顿。

### 3.2 实现建议

复用 `PDF_Viewer` 内部已加载的 pdfjs 文档与缩放，**不二次加载 PDF**：

- 高亮数据（`props.highlights`）增加可选字段 `text?: string`（或沿用现有 excerpt 类字段），docs-ui 优先展示该文字；
- 建议再增加可选字段 `matchText?: string` 标注“雷同段”，docs-ui 在浮框内对该段加粗高亮；未提供时尝试在 `text` 中定位，或不做高亮；
- `text` 为空时，用内部 pdfjs 文档按高亮 bbox 精确取字（`page.getTextContent()` + `Util.transform`，按页缓存），覆盖 bbox 区域内的全部文本；
- 浮框由 `PDF_Viewer` 内置渲染，跟随高亮框/光标定位，自动翻转防溢出；
- 也可提供 `@highlight-hover` 事件或 slot，让消费方自定义浮框内容与样式。

性能要求：

- 文本内容按页缓存，不重复 `getTextContent()`；
- hover 处理轻量（仅高亮框触发，无需全局 mousemove 扫描）。

### 3.3 对外接口建议

新增 props（向后兼容，默认值明确文档化）：

| prop | 类型 | 说明 |
|------|------|------|
| `highlightHoverText` | `boolean` | 是否启用高亮悬停原文，默认 `true` |
| `highlightHoverFontSize` | `number` | 浮框字号，默认 `13` |
| `highlightHoverMaxWidth` / `highlightHoverMaxHeight` | `number` | 浮框尺寸上限，超出滚动，默认 `340` / `180` |

`highlights` 条目建议支持可选 `text` 字段；若消费方不传，docs-ui 内部按 bbox 提取。

### 3.4 验收标准

1. 点击右侧证据后，悬停任一高亮框 ≤100ms 内出现原文浮框，文字与高亮区域一致；
2. 浮框展示 bbox 全部内容，且“雷同段”有加粗/高亮标识；
3. 悬停普通文字区域不出现浮框；
4. 亮色/暗色主题下浮框样式正常；
5. 翻页、缩放、搜索、拖拽过程中无残留浮框、无明显性能卡顿；
6. 不再触发消费方额外的 PDF 加载请求。

## 4. 交付物

- `packages/docs-ui` 源码改动（`src/components/common/viewers/PDF_Viewer.vue` 及相关类型/样式）；
- README 补充新 props 与行为说明；
- 发版并提供版本号（npm 包或内部发布均可）；
- DredgeAI 侧收到新版本后：更新 `vendor/angineer-docs-ui` 副本 → 删除消费侧 `PdfViewer.vue` 中的本地兜底 → 改为通过 props 使用。

## 5. 备注

- DredgeAI 当前兜底实现位于 `user-web/src/views/ai-bid/compare/components/PdfViewer.vue`（wrapper 层），上游落地后可整体移除；
- 参考文件：上游 `D:\AI\AnGIneer\packages\docs-ui\src\components\common\viewers\PDF_Viewer.vue`，DredgeAI vendor 副本 `D:\AI\DredgeAI\vendor\angineer-docs-ui`。

> ⚠️ **DOM 结构约定（请勿静默改动）**：在 docs-ui 提供内置浮框之前，DredgeAI 的 wrapper 依赖 `PDF_Viewer` 以下内部实现做事件委托与坐标匹配，请重构时保持稳定，或在改动前与 DredgeAI 同步：
>
> 1. `.pdf-highlight-box` 高亮框元素（事件委托入口）；
> 2. `.pdf-page-wrapper` 页面容器与 `canvas[data-page]` 的页码属性（定位到具体页）；
> 3. 高亮框按“面积降序”排序渲染（wrapper 用归一化矩形中心做最近匹配，不依赖 DOM 顺序，但依赖 bbox 几何与页面坐标系）。
>
> 若这些结构必然要改，请优先把“高亮悬停显示原文”内置进 `PDF_Viewer`（即本需求正文），DredgeAI 即可随版本升级移除 wrapper，不再有耦合。
