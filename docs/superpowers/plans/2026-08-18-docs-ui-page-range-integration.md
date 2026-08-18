# docs-ui v0.1.2 集成与 PDF page-range 上游化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** DredgeAI 消费端切换到 docs-ui v0.1.2 内置的高亮悬停原文能力，并在 docs-ui 上游（v0.1.3）实现 `pdfPageRange` 子集渲染模式与 `pdf-loaded` 事件，最终让 compare 证据页视图复用 `PDF_Viewer`。

**Architecture:** 分三个阶段。Phase 1 在 DredgeAI 升 submodule 到 v0.1.2 并删除 `PdfViewer.vue` 本地兜底（以 MutationObserver 观察首个 canvas 作为临时加载信号）。Phase 2 在 AnGIneer monorepo `packages/docs-ui` 用 TDD 添加 `src/utils/pageRange.ts` 纯函数并接入 `PDF_Viewer` 虚拟滚动层，同时补 `pdf-loaded` 事件，发布 v0.1.3。Phase 3 待 v0.1.3 可用后，DredgeAI 用 `pdf-page-range` + `@pdf-loaded` 替换证据页自绘实现并删除 `PdfRangeViewer.vue`。

**Tech Stack:** Vue 3.5 / ant-design-vue 4 / pdfjs-dist 6.2 / pnpm 9 / TypeScript strict / node:test + tsx（docs-ui 测试）。

---

## 文件结构

### DredgeAI（Phase 1/3）

- Modify: `vendor/angineer-docs-ui`（submodule gitlink，锁 v0.1.2 / v0.1.3）
- Modify: `pnpm-lock.yaml`（`file:` 依赖版本刷新）
- Modify: `user-web/src/views/ai-bid/compare/components/PdfViewer.vue`（删兜底、excerpt 透传、临时 loaded 信号、后续换 `pdf-page-range` + `@pdf-loaded`）
- Modify: `admin-web/src/views/data/static/standards/components/StandardPdfViewer.vue`（删 `office-preview-url`）
- Modify: `user-web/src/views/ai-bid/compare/components/PdfWorkspace.vue`（证据页模式改用 `PdfViewer` + `:page-range`）
- Delete: `user-web/src/views/ai-bid/compare/components/PdfRangeViewer.vue`（Phase 3）

### docs-ui 上游（Phase 2，AnGIneer monorepo `packages/docs-ui`）

- Create: `src/utils/pageRange.ts`（纯函数）
- Test: `test/pageRange.test.ts`
- Modify: `src/components/common/viewers/PDF_Viewer.vue`（prop/事件/虚拟滚动层）
- Modify: `README.md`、`CHANGELOG.md`、`package.json`（0.1.3）

---

## Phase 0：基线核对

### Task 0: 确认 DredgeAI 工作区与 vendor 基线

**Files:**
- 检查：`D:\AI\DredgeAI`

- [ ] **Step 1: 记录当前 git 状态**

Run: `git status --short`
Expected: 仅存在已知未提交改动（`user-web/src/router/manifests.ts`、`user-web/src/views/ai-bid/compare/components/PdfWorkspace.vue`、`UnifyCompareTable.vue`、`index.vue` 为 M；`PdfRangeViewer.vue` 为 ??）。后续所有 DredgeAI 提交必须用显式路径 `git add -- <paths>`，禁止 `git add -A`。

- [ ] **Step 2: 确认 vendor 锁点与上游 tag 可用**

Run: `git -C vendor/angineer-docs-ui rev-parse HEAD`
Expected: `4b96e23...`。再 Run: `git -C vendor/angineer-docs-ui tag -l v0.1.2`，Expected: `v0.1.2`（若缺失先 `git -C vendor/angineer-docs-ui fetch --tags origin`）。

---

## Phase 1：DredgeAI v0.1.2 集成

### Task 1: 升级 submodule 到 v0.1.2

**Files:**
- Modify: `vendor/angineer-docs-ui`（gitlink）
- Modify: `pnpm-lock.yaml`

- [ ] **Step 1: 切换 submodule 到 v0.1.2**

Run: `git -C vendor/angineer-docs-ui checkout v0.1.2`
Expected: detached HEAD at `7d861a2`。Run: `git -C vendor/angineer-docs-ui rev-parse HEAD`，Expected: `7d861a2...`。

- [ ] **Step 2: 校验包版本**

Run: `Get-Content vendor/angineer-docs-ui/package.json | Select-String '"version"'`
Expected: `"version": "0.1.2"`。

- [ ] **Step 3: 刷新 pnpm 锁文件**

Run: `pnpm install`
Expected: 成功，`pnpm-lock.yaml` 中 `@angineer/docs-ui` 的 `version: 0.1.2`。Run: `git diff --stat pnpm-lock.yaml` 确认仅有依赖元数据变化。

- [ ] **Step 4: 提交（仅 submodule gitlink + lockfile）**

```bash
git add -- vendor/angineer-docs-ui pnpm-lock.yaml
git commit -m "chore(deps): bump @angineer/docs-ui submodule to v0.1.2"
```
Expected: husky 钩子自动跑 `pnpm --filter user-web typecheck && pnpm --filter admin-web typecheck`，通过后提交成功。提交后 `git status --short` 仍显示用户的其他未提交改动（不允许出现）。

### Task 2: user-web `PdfViewer.vue` 移除本地兜底

**Files:**
- Modify: `user-web/src/views/ai-bid/compare/components/PdfViewer.vue`

- [ ] **Step 1: 删除本地 hover 兜底的全部 JS**

在 `<script setup>` 中按名称删除以下项（完整实现均在当前文件内，勿动其他代码）：

- 两行 pdfjs 导入：`import * as pdfjsLib from 'pdfjs-dist'`、`import pdfjsWorker from 'pdfjs-dist/build/pdf.worker.min.mjs?url'`
- 一行 worker 配置：`pdfjsLib.GlobalWorkerOptions.workerSrc = pdfjsWorker`
- 类型与状态：`HoverTextItem`、`HoverPageData`、`pdfDoc`、`pdfLoadingTask`、`pageDataCache`、`HoverSegment`、`hoverSegments`、`hoverVisible`、`hoverPos`、`hoverWidth`、`hoverStyle`
- 函数：`disposePdfDoc`、`loadPageData`、`onHoverOut`、`onHoverOver`、`showBoxPopup`、`buildSegments`、`splitAt`、`mapCompactIndex`、`matchHighlight`、`textInRect`
- 旧的文件加载 watch：`watch(() => props.fileUrl, (url) => { ...pdfjs getDocument... }, { immediate: true })`（Step 3 会用新 watch 替代）
- 生命周期：`onBeforeUnmount(disposePdfDoc)`

同时把 Vue import 从 `import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'` 改为 `import { computed, ref } from 'vue'`（`nextTick`/`onBeforeUnmount`/`watch` 由 Step 3 的新代码按需加回）。

- [ ] **Step 2: 替换模板（删除浮框、hover 事件、死注释，加 bodyRef）**

将整个 `<template>` 替换为：

```vue
<template>
  <div class="pdf-viewer" :class="{ 'pdf-viewer-scanning': scanning }">
    <div ref="bodyRef" class="pdf-viewer__body">
      <EmptyState v-if="!fileUrl" type="no-data" title="请选择文档。" />

      <EmptyState
        v-else-if="!canPreviewPdf"
        type="no-data"
        title="暂不支持在线预览"
        description="Word 文档请下载后在本机查看。"
      />

      <PDF_Viewer
        v-else
        class="pdf-viewer__viewer"
        :node="node"
        :theme="theme"
        :is-pdf="true"
        :is-office="false"
        :is-image="false"
        :is-text="false"
        :file-url="fileUrl"
        :pdf-viewer-url="fileUrl"
        text-content=""
        :current-pdf-page="page"
        :pdf-page-count="totalPages && totalPages > 0 ? totalPages : undefined"
        :highlights="highlights"
        :active-highlight-id="activeHighlightId"
        :text-scroll-percent="0"
        @pdf-active-page="emit('update:page', $event)"
      />
    </div>
  </div>
</template>
```

要求：删掉整个 `<!-- OLD_TEMPLATE_BELOW ... -->` 死注释块；删掉 `@mouseover="onHoverOver"` / `@mouseout="onHoverOut"`；删掉 `.pdf-hover-pop` 浮框 div；不再出现 `office-preview-url`。

- [ ] **Step 3: 替换 `<script setup>` 主体**

保留 props/emits/`canPreviewPdf`/`activeHighlightId`/`node`，将 highlights 映射补上 `excerpt`，并把整个 hover 段换成 loaded 信号。最终脚本为：

```ts
<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'

import { PDF_Viewer } from '@angineer/docs-ui'
import '@angineer/docs-ui/style'
import { EmptyState } from '@shared/web'
import { normalizeRect } from '@shared/types'
import { useThemeStore } from '@shared/web/stores'
import { isPdfFileName } from '../constants'
import type { BlockRange } from '@/types'

const props = withDefaults(defineProps<{
  fileUrl: string
  title?: string
  page?: number
  totalPages?: number
  high?: BlockRange[]
  scanning?: boolean
  activeHighlightId?: string | null
  /** 隐藏引用库 PDF_Viewer 标题栏左侧的「原文」标签（库源码不可改，经 CSS 覆盖） */
  hideOriginalLabel?: boolean
}>(), {
  page: 1,
  high: () => [],
  scanning: false,
  activeHighlightId: null,
  hideOriginalLabel: false,
})

const emit = defineEmits<{
  'update:page': [value: number]
  'loaded': [url: string]
}>()

interface ViewerNode {
  status: string
  filePath: string
}

interface ViewerHighlight {
  id: string
  itemId: string
  page: number
  hasRect: boolean
  left: number
  top: number
  width: number
  height: number
  lineStart: number | null
  lineEnd: number | null
  type?: string
  excerpt?: string
}

const themeStore = useThemeStore()
const theme = computed(() => themeStore.effectiveTheme)

/** Word 文档同样允许在线预览：后端文件接口会对 doc/docx 返回转换后的 PDF（首次转换失败则回退原文）。 */
const canPreviewPdf = computed(() =>
  isPdfFileName(props.title) || /\.(?:doc|docx)$/i.test(props.title ?? ''))

/** 定位证据时整组高亮同属一个 pairId，未显式指定则取第一组作为激活态。 */
const activeHighlightId = computed(() =>
  props.activeHighlightId ?? props.high.find((h) => h.pairId)?.pairId ?? null,
)

const node = computed<ViewerNode>(() => ({
  status: 'completed',
  filePath: props.fileUrl,
}))

/** bbox 为 0~1 归一化坐标，PDF_Viewer 高亮层按 0~1 比例直接渲染。 */
const highlights = computed<ViewerHighlight[]>(() =>
  props.high.map((h, i) => {
    const [x0, y0, x1, y1] = normalizeRect(h.bbox)
    return {
      id: `${h.pairId ?? h.docId}-${i}`,
      itemId: h.pairId ?? h.docId,
      page: h.page,
      hasRect: true,
      left: x0,
      top: y0,
      width: x1 - x0,
      height: y1 - y0,
      lineStart: null,
      lineEnd: null,
      type: 'text',
      excerpt: h.excerpt,
    }
  }),
)

/* —— loaded 加载信号：docs-ui v0.1.2 尚无官方事件，观察首个 canvas 出现；
   v0.1.3 提供 pdf-loaded 事件后替换为 @pdf-loaded（见 Phase 3 Task 10）。 —— */
const bodyRef = ref<HTMLElement | null>(null)
let loadedObserver: MutationObserver | null = null
let loadedUrl = ''

function notifyLoaded(): void {
  if (loadedUrl === props.fileUrl) return
  loadedUrl = props.fileUrl
  emit('loaded', props.fileUrl)
}

function attachLoadedSignal(): void {
  loadedObserver?.disconnect()
  loadedObserver = null
  const el = bodyRef.value
  if (!el || !props.fileUrl) return
  loadedObserver = new MutationObserver(() => {
    if (el.querySelector('canvas[data-page]')) notifyLoaded()
  })
  loadedObserver.observe(el, { childList: true, subtree: true })
  if (el.querySelector('canvas[data-page]')) notifyLoaded()
}

watch(() => props.fileUrl, () => {
  loadedUrl = ''
  void nextTick(attachLoadedSignal)
}, { immediate: true })

onBeforeUnmount(() => {
  loadedObserver?.disconnect()
  loadedObserver = null
})
</script>
```

- [ ] **Step 4: 删除 `.pdf-hover-pop` 样式**

在 `<style scoped lang="less">` 中删除整个 `.pdf-hover-pop` 及其 `&__match` 子规则块。其余样式（`.pdf-viewer`、`.pdf-viewer__body`、`.pdf-viewer__viewer` 等）与第二个全局 `<style>`（`.pane-title-prefix`、`.pdf-virtual-spacer` 修复）保持不变。

- [ ] **Step 5: 类型检查**

Run: `pnpm --filter user-web typecheck`
Expected: PASS（无未使用变量/参数错误）。

- [ ] **Step 6: 提交**

```bash
git add -- user-web/src/views/ai-bid/compare/components/PdfViewer.vue
git commit -m "feat(user-web): 移除 PDF 高亮悬停本地兜底，改用 docs-ui v0.1.2 内置浮框"
```

### Task 3: admin-web `StandardPdfViewer.vue` 清理

**Files:**
- Modify: `admin-web/src/views/data/static/standards/components/StandardPdfViewer.vue`

- [ ] **Step 1: 删除 `office-preview-url=""`**

删除模板中 `<PDF_Viewer ... office-preview-url="" text-content="" ...>` 里的 `office-preview-url=""` 一行。hover 由 docs-ui v0.1.2 默认开启，无需其他改动。

- [ ] **Step 2: 类型检查并提交**

Run: `pnpm --filter admin-web typecheck`
Expected: PASS。

```bash
git add -- admin-web/src/views/data/static/standards/components/StandardPdfViewer.vue
git commit -m "fix(admin-web): 移除 docs-ui 已删除的 office-preview-url prop"
```

### Task 4: Phase 1 手工回归

**Files:**
- 无（浏览器验证）

- [ ] **Step 1: 启动并验证 compare 页**

Run: `pnpm dev:user`，打开比标分析页，加载带证据的任务。逐项核对：

1. 点击右侧证据后，左侧/右侧 PDF 高亮定位正常（含文档加载中点击证据、加载完成后自动重放）。
2. 悬停任一高亮框 ≤300ms 出浮框，显示 bbox 原文；有 `excerpt` 的证据命中段加粗。
3. 悬停普通文字区域不出现浮框；鼠标移出浮框消失，无残留。
4. 翻页、缩放、搜索、重新定位后浮框消失，无卡顿。
5. 明暗主题下浮框样式正常。
6. 网络面板确认同一 PDF 只发一次加载请求（对比升级前二次加载）。

- [ ] **Step 2: 验证 admin-web 标准库预览**

Run: `pnpm dev:admin`，打开标准库详情预览 PDF，确认悬停高亮框可显示原文（走内部 bbox 取字），明暗主题正常。

---

## Phase 2：docs-ui 上游 v0.1.3（工作目录 `D:\AI\AnGIneer`）

### Task 5: pageRange 纯函数（TDD）

**Files:**
- Create: `D:\AI\AnGIneer\packages\docs-ui\test\pageRange.test.ts`
- Create: `D:\AI\AnGIneer\packages\docs-ui\src\utils\pageRange.ts`

- [ ] **Step 1: 写失败测试**

```ts
import { test } from 'node:test'
import assert from 'node:assert/strict'

import { clampPageToRange, normalizePageRange } from '../src/utils/pageRange.ts'

test('normalizePageRange undefined/空数组返回整篇', () => {
  assert.deepEqual(normalizePageRange(undefined, 5), [1, 2, 3, 4, 5])
  assert.deepEqual(normalizePageRange([], 5), [1, 2, 3, 4, 5])
})

test('normalizePageRange 去重、排序、过滤越界与非数字', () => {
  assert.deepEqual(normalizePageRange([7, 3, 3, 0, -1, 100], 5), [3])
})

test('normalizePageRange 全部越界退化为整篇', () => {
  assert.deepEqual(normalizePageRange([0, 99], 4), [1, 2, 3, 4])
})

test('clampPageToRange 边界与就近吸附', () => {
  const range = [3, 4, 7]
  assert.equal(clampPageToRange(1, range), 3)
  assert.equal(clampPageToRange(9, range), 7)
  assert.equal(clampPageToRange(4, range), 4)
  assert.equal(clampPageToRange(5, range), 4)
  assert.equal(clampPageToRange(6, range), 7)
})

test('clampPageToRange 距离相等取较小页', () => {
  assert.equal(clampPageToRange(4, [3, 5]), 3)
})

test('clampPageToRange 空数组返回 1', () => {
  assert.equal(clampPageToRange(4, []), 1)
})
```

- [ ] **Step 2: 运行测试确认失败**

Run（在 `D:\AI\AnGIneer\packages\docs-ui`）: `pnpm dlx tsx --test test/pageRange.test.ts`
Expected: FAIL，报 `Cannot find module '../src/utils/pageRange.ts'`。

- [ ] **Step 3: 实现纯函数**

```ts
/** 归一化 pdfPageRange：去重、升序、过滤 1..total 之外的页码；
 *  undefined/空数组/全部越界时退化为整篇 [1..total]。 */
export function normalizePageRange(range: number[] | undefined, total: number): number[] {
  const count = Math.max(1, Math.floor(total) || 1)
  if (!Array.isArray(range) || range.length === 0) {
    return Array.from({ length: count }, (_, i) => i + 1)
  }
  const set = new Set<number>()
  for (const raw of range) {
    const page = Math.floor(raw)
    if (Number.isFinite(page) && page >= 1 && page <= count) set.add(page)
  }
  if (set.size === 0) {
    return Array.from({ length: count }, (_, i) => i + 1)
  }
  return [...set].sort((a, b) => a - b)
}

/** 将任意页码吸附到最近子集页；距离相等取较小页；空数组返回 1。 */
export function clampPageToRange(page: number, range: number[]): number {
  const list = Array.isArray(range) ? range : []
  if (!list.length) return 1
  const value = Math.round(Number(page))
  if (!Number.isFinite(value)) return list[0]
  if (value <= list[0]) return list[0]
  if (value >= list[list.length - 1]) return list[list.length - 1]
  let best = list[0]
  let bestDist = Number.POSITIVE_INFINITY
  for (const candidate of list) {
    const dist = Math.abs(candidate - value)
    if (dist < bestDist || (dist === bestDist && candidate < best)) {
      bestDist = dist
      best = candidate
    }
  }
  return best
}
```

- [ ] **Step 4: 运行测试确认通过**

Run: `pnpm dlx tsx --test test/pageRange.test.ts`
Expected: PASS（6 个测试全绿）。

- [ ] **Step 5: 提交（AnGIneer monorepo）**

```bash
git add -- packages/docs-ui/src/utils/pageRange.ts packages/docs-ui/test/pageRange.test.ts
git commit -m "feat(docs-ui): page range 归一化与就近吸附纯函数"
```

### Task 6: `PDF_Viewer` 接入 `pdfPageRange`

**Files:**
- Modify: `D:\AI\AnGIneer\packages\docs-ui\src\components\common\viewers\PDF_Viewer.vue`

- [ ] **Step 1: 导入纯函数**

在文件顶部 import 区新增：

```ts
import { clampPageToRange, normalizePageRange } from '../../../utils/pageRange'
```

- [ ] **Step 2: 声明 prop**

在 `defineProps<{ ... }>` 中 `pdfPageCount?: number` 附近新增：

```ts
pdfPageRange?: number[]
```

`withDefaults` 中**不**加默认值（undefined = 整篇）。

- [ ] **Step 3: 滚动层加 `activePageRange` 计算属性**

在 `usePdfVirtualScroll` 内、`displayPdfPageCount` 之后新增：

```ts
const activePageRange = computed<number[]>(() => normalizePageRange(props.pdfPageRange, displayPdfPageCount.value))
```

- [ ] **Step 4: `clampPage` 改为子集吸附**

将函数体替换为：

```ts
function clampPage(value: number) {
  return clampPageToRange(value, activePageRange.value)
}
```

- [ ] **Step 5: `pageLayout` 只布局子集页**

在 `_cachedEstHeight` 声明旁新增 `let _cachedRangeKey = ''`。将 `pageLayout` 计算体替换为：

```ts
const pageLayout = computed(() => {
  const count = displayPdfPageCount.value
  const rangeKey = activePageRange.value.join(',')
  if (!_layoutDirty && _cachedLayout && count === _cachedPageCount && estimatedPageHeight.value === _cachedEstHeight && rangeKey === _cachedRangeKey) {
    return _cachedLayout
  }
  const topByPage: number[] = []
  let cursor = VERTICAL_PADDING
  const pages = activePageRange.value
  for (const page of pages) {
    topByPage[page] = cursor
    const ph = pageHeights[page]
    cursor += (ph > 0) ? ph : estimatedPageHeight.value
    if (page !== pages[pages.length - 1]) cursor += PAGE_GAP
  }
  _cachedLayout = { topByPage, totalHeight: Math.max(1, cursor + VERTICAL_PADDING) }
  _cachedPageCount = count
  _cachedEstHeight = estimatedPageHeight.value
  _cachedRangeKey = rangeKey
  _layoutDirty = false
  return _cachedLayout
})
```

- [ ] **Step 6: `updateRenderedPageRange` 按子集计算**

将函数体替换为：

```ts
function updateRenderedPageRange() {
  const container = pdfScrollRef.value
  const layout = pageLayout.value
  virtualContentHeight.value = layout.totalHeight
  const pages = activePageRange.value
  const firstPage = pages[0]
  const lastPage = pages[pages.length - 1]
  if (!container || !props.isPdf) {
    renderedPageRange.start = firstPage
    renderedPageRange.end = lastPage
    return
  }
  if (pages.length <= 1) { renderedPageRange.start = firstPage; renderedPageRange.end = firstPage; return }
  const viewportTop = container.scrollTop
  const viewportBottom = viewportTop + container.clientHeight
  let firstVisibleIndex = -1
  let lastVisibleIndex = -1
  for (const page of pages) {
    const pageTop = layout.topByPage[page] || 0
    const pageBottom = pageTop + pageHeightOf(page) + PAGE_GAP
    const intersectsViewport = pageBottom >= viewportTop && pageTop <= viewportBottom
    if (intersectsViewport) {
      if (firstVisibleIndex === -1) firstVisibleIndex = page
      lastVisibleIndex = page
    }
  }
  if (firstVisibleIndex === -1 || lastVisibleIndex === -1) {
    let closestPage = firstPage
    let minDiff = Number.POSITIVE_INFINITY
    for (const page of pages) {
      const diff = Math.abs((layout.topByPage[page] || 0) - viewportTop)
      if (diff < minDiff) { minDiff = diff; closestPage = page }
    }
    renderedPageRange.start = Math.max(firstPage, closestPage - RENDER_BUFFER)
    renderedPageRange.end = Math.min(lastPage, closestPage + RENDER_BUFFER)
    return
  }
  renderedPageRange.start = Math.max(firstPage, firstVisibleIndex - RENDER_BUFFER)
  renderedPageRange.end = Math.min(lastPage, lastVisibleIndex + RENDER_BUFFER)
}
```

- [ ] **Step 7: `resolveViewportPage` 按子集取最近页**

将函数体替换为：

```ts
function resolveViewportPage(scrollTop: number, clientHeight: number) {
  const viewportCenter = scrollTop + (clientHeight / 2)
  let bestPage = activePageRange.value[0]
  let minDistance = Number.POSITIVE_INFINITY
  const layout = pageLayout.value
  for (const page of activePageRange.value) {
    const top = layout.topByPage[page] || 0
    const center = top + (pageHeightOf(page) / 2)
    const distance = Math.abs(center - viewportCenter)
    if (distance < minDistance) { minDistance = distance; bestPage = page }
  }
  return bestPage
}
```

- [ ] **Step 8: 返回值导出 `activePageRange`**

在 `usePdfVirtualScroll` 的 `return { ... }` 中 `displayPdfPageCount` 附近新增 `activePageRange`。

- [ ] **Step 9: 组件顶层加导航能力计算与 range 监听**

在组件 `<script setup>` 顶层（`activePdfPage` / `displayPdfPageCount` 解构之后）新增：

```ts
const hasPrevPdfPage = computed(() => {
  const pages = scroll.activePageRange.value
  return pages.length > 1 && activePdfPage.value > pages[0]
})
const hasNextPdfPage = computed(() => {
  const pages = scroll.activePageRange.value
  return pages.length > 1 && activePdfPage.value < pages[pages.length - 1]
})
watch(() => props.pdfPageRange, () => {
  scroll.invalidateLayout()
  scroll.scheduleRenderedPageRangeUpdate()
  scroll.scrollToPdfPage(activePdfPage.value, 'auto')
})
```

- [ ] **Step 10: 文档重置时起始页吸附到子集第一页**

在 `watch([normalizedPdfSource, () => props.isPdf], ...)` 重置块中，把 `scroll.activePdfPage.value = 1` 替换为：

```ts
scroll.activePdfPage.value = scroll.activePageRange.value[0] || 1
```

- [ ] **Step 11: 工具栏 prev/next 禁用条件**

模板中上一页按钮 `:disabled="activePdfPage <= 1"` 改为 `:disabled="!hasPrevPdfPage"`；下一页按钮 `:disabled="activePdfPage >= displayPdfPageCount"` 改为 `:disabled="!hasNextPdfPage"`。

- [ ] **Step 12: 类型检查**

Run（`D:\AI\AnGIneer\packages\docs-ui`）: `pnpm dlx vue-tsc --noEmit`
Expected: PASS。

### Task 7: `pdf-loaded` 事件

**Files:**
- Modify: `D:\AI\AnGIneer\packages\docs-ui\src\components\common\viewers\PDF_Viewer.vue`

- [ ] **Step 1: `usePdfDocument` 增加成功回调**

在 `usePdfDocument` 的 `shared` 类型中新增 `onDocumentLoaded?: () => void`；在 `onPdfDocumentLoaded` 成功路径末尾（`localPdfPageCount.value = ...` 与 firstPage 测量逻辑之后）新增：

```ts
shared.onDocumentLoaded?.()
```

- [ ] **Step 2: `defineEmits` 声明事件**

```ts
'pdf-loaded': [source: string]
```

- [ ] **Step 3: 组件调用处传入回调**

`const doc = usePdfDocument({ ... }, scroll, zoom, render)` 的 shared 对象中新增：

```ts
onDocumentLoaded: () => emit('pdf-loaded', props.fileUrl || props.pdfViewerUrl.split('#')[0] || props.pdfViewerUrl),
```

- [ ] **Step 4: 类型检查**

Run（`D:\AI\AnGIneer\packages\docs-ui`）: `pnpm dlx vue-tsc --noEmit`
Expected: PASS。

### Task 8: 文档、版本与发布 v0.1.3

**Files:**
- Modify: `D:\AI\AnGIneer\packages\docs-ui\README.md`
- Modify: `D:\AI\AnGIneer\packages\docs-ui\CHANGELOG.md`
- Modify: `D:\AI\AnGIneer\packages\docs-ui\package.json`

- [ ] **Step 1: README 补充接口**

在 PDF 高亮悬停章节后追加：

```markdown
## 17. PDF 子集渲染与加载事件（v0.1.3）

`pdf-page-range?: number[]`：绝对页码数组，仅渲染这些页（虚拟滚动、工具栏、高亮、
悬停取字均按子集工作）；undefined 或空数组 = 整篇；越界页码自动过滤，全部越界时
退化为整篇。页码输入框与 `current-pdf-page` 均使用绝对页码，子集外输入就近吸附。

`pdf-loaded` 事件：文档加载完成后触发，payload 为当前源 URL，可用于加载完成后
重放定位等场景。
```

- [ ] **Step 2: CHANGELOG 追加**

```markdown
## 0.1.3

- feat: PDF_Viewer 支持 pdfPageRange 子集渲染（绝对页码、越界吸附）
- feat: 新增 pdf-loaded 加载完成事件
```

- [ ] **Step 3: 版本号 0.1.2 → 0.1.3**

修改 `package.json` 的 `"version": "0.1.2"` 为 `"version": "0.1.3"`。

- [ ] **Step 4: 全量自测**

Run（`D:\AI\AnGIneer\packages\docs-ui`）: `pnpm dlx tsx --test test/pageRange.test.ts test/pdfHoverText.test.ts test/citationTarget.test.ts test/highlightGroup.test.ts test/display-roots.test.mjs`
Expected: PASS（既有测试与新增 pageRange 测试全部通过）。

- [ ] **Step 5: 提交 monorepo**

```bash
git add -- packages/docs-ui
git commit -m "feat(docs-ui): PDF page-range 子集模式与 pdf-loaded 事件，版本 0.1.3"
```

- [ ] **Step 6: 同步独立仓库并推送**

Run（`D:\AI\AnGIneer`，需在 main 分支）:
`powershell -ExecutionPolicy Bypass -File scripts/sync-standalone.ps1 -Message "feat(docs-ui): PDF page-range 子集模式与 pdf-loaded 事件，版本 0.1.3"`
Expected: main 与 `angineer-docs-ui` 均推送成功。若 `.worktrees/angineer-docs-ui` 非干净，先处理该 worktree。

- [ ] **Step 7: 打 tag 并推送**

```bash
git -C .worktrees/angineer-docs-ui tag v0.1.3
git -C .worktrees/angineer-docs-ui push angineer-docs-ui v0.1.3
```

Expected: 远端出现 `refs/tags/v0.1.3`。Run: `git ls-remote --tags https://github.com/0mao0/angineer-docs-ui` 确认。

---

## Phase 3：DredgeAI 证据页切换（v0.1.3 可用后）

### Task 9: 升 submodule 到 v0.1.3

**Files:**
- Modify: `vendor/angineer-docs-ui`（gitlink）、`pnpm-lock.yaml`

- [ ] **Step 1: 拉取并切换 tag**

```bash
git -C vendor/angineer-docs-ui fetch --tags origin
git -C vendor/angineer-docs-ui checkout v0.1.3
pnpm install
```

Expected: `git -C vendor/angineer-docs-ui rev-parse HEAD` 指向 v0.1.3 提交；lockfile 刷新。

- [ ] **Step 2: 提交**

```bash
git add -- vendor/angineer-docs-ui pnpm-lock.yaml
git commit -m "chore(deps): bump @angineer/docs-ui submodule to v0.1.3"
```

### Task 10: `PdfViewer.vue` 换官方事件并透传 `page-range`

**Files:**
- Modify: `user-web/src/views/ai-bid/compare/components/PdfViewer.vue`

- [ ] **Step 1: 删除 MutationObserver 临时方案**

删除 `bodyRef`、`loadedObserver`、`loadedUrl`、`notifyLoaded`、`attachLoadedSignal`、`watch(() => props.fileUrl, ...)`、`onBeforeUnmount` 及其 import。

- [ ] **Step 2: 新增 `page-range` prop 并绑定事件**

props 新增：

```ts
pageRange?: number[]
```

`PDF_Viewer` 模板新增 `:pdf-page-range="pageRange?.length ? pageRange : undefined"`，并将 `@pdf-active-page="emit('update:page', $event)"` 旁新增：

```vue
@pdf-loaded="(url) => emit('loaded', url)"
```

- [ ] **Step 3: 类型检查并提交**

Run: `pnpm --filter user-web typecheck`
Expected: PASS。

```bash
git add -- user-web/src/views/ai-bid/compare/components/PdfViewer.vue
git commit -m "feat(user-web): 切换 docs-ui pdf-loaded 事件并透传 page-range"
```

### Task 11: `PdfWorkspace` 证据页改用 `PdfViewer` 并删除 `PdfRangeViewer`

**Files:**
- Modify: `user-web/src/views/ai-bid/compare/components/PdfWorkspace.vue`
- Delete: `user-web/src/views/ai-bid/compare/components/PdfRangeViewer.vue`

- [ ] **Step 1: 模板中证据页模式替换**

把 `viewMode === 'pages'` 分支中的两个 `PdfRangeViewer` 换成与 full 模式一致的 `PdfViewer`，仅多传 `:page-range`：

```vue
<template v-if="viewMode === 'pages'">
  <PdfViewer
    :file-url="docFileUrl(leftDocId)"
    :title="docName(leftDocId)"
    :page="leftPage"
    :total-pages="docPages(leftDocId)"
    :high="leftHigh"
    :page-range="leftRange"
    :scanning="scanningDocId === leftDocId"
    hide-original-label
    @update:page="leftPage = $event"
    @loaded="(url) => onViewerLoaded(leftDocId, url)"
  />
  <PdfViewer
    v-if="!singlePane"
    :file-url="docFileUrl(rightDocId)"
    :title="docName(rightDocId)"
    :page="rightPage"
    :total-pages="docPages(rightDocId)"
    :high="rightHigh"
    :page-range="rightRange"
    :scanning="scanningDocId === rightDocId"
    hide-original-label
    @update:page="rightPage = $event"
    @loaded="(url) => onViewerLoaded(rightDocId, url)"
  />
</template>
```

同时删除 `import PdfRangeViewer from './PdfRangeViewer.vue'`。`viewMode` 切换、`MAX_PAGES_MODE = 24` 回退逻辑、`leftRange/rightRange` 计算保持不变。

- [ ] **Step 2: 删除文件**

```bash
git rm -- user-web/src/views/ai-bid/compare/components/PdfRangeViewer.vue
```

- [ ] **Step 3: 类型检查并提交**

Run: `pnpm --filter user-web typecheck`
Expected: PASS（确认无残留 `PdfRangeViewer` 引用）。

```bash
git add -- user-web/src/views/ai-bid/compare/components/PdfWorkspace.vue
git commit -m "feat(user-web): 证据页视图改用 PDF_Viewer page-range 模式"
```

### Task 12: Phase 3 回归

**Files:**
- 无（浏览器验证）

- [ ] **Step 1: compare 页回归**

Run: `pnpm dev:user`，核对：

1. 点击证据：跨页 ≤24 时进入证据页模式，只渲染 refs 覆盖页；跨页 >24 自动退回全篇。
2. 证据页模式下悬停高亮框出原文浮框；工具栏页码为绝对页码，prev/next 子集内跳转、边界禁用。
3. 全篇/证据页切换正常；双文档定位正常。
4. 明暗主题正常；加载中点击证据的定位重放正常（走 `@pdf-loaded`）。

- [ ] **Step 2: 双端构建**

Run: `pnpm build`
Expected: user-web 与 admin-web 构建均通过。

---

## 自审记录

- 规格覆盖：Phase 1 = 规格 §4（submodule、PdfViewer、StandardPdfViewer、验证）；Phase 2 = 规格 §5（`pdfPageRange` 行为、`pdf-loaded`、纯函数测试、README/CHANGELOG/版本、发布）；Phase 3 = 规格 §6（切换、删除 PdfRangeViewer、24 页回退保留）。§7 边界（空/越界/空结果退化、文档切换重置、MutationObserver 清理）分别落在 Task 5/6/2/10。§8 风险（不覆盖未提交改动、office-preview-url、主题回归、时序依赖）落在 Task 0/3/4/12。
- 类型一致性：docs-ui 侧 prop `pdfPageRange`、事件 `pdf-loaded`；DredgeAI wrapper 侧 prop `pageRange`、事件 `loaded`（对外不变）。纯函数 `normalizePageRange`/`clampPageToRange` 命名在 Task 5 与 Task 6 中一致。
- 无占位符：所有代码步骤均给出完整代码或精确删除清单；手工回归步骤给出可执行核对项。
