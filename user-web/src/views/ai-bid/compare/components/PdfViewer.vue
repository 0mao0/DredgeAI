<template>
  <div class="pdf-viewer" :class="{ 'pdf-viewer-scanning': scanning }">
    <div
      class="pdf-viewer__body"
      @mouseover="onHoverOver"
      @mouseout="onHoverOut"
    >
      <EmptyState v-if="!fileUrl" type="no-data" title="请选择文档" />

      <EmptyState
        v-else-if="!canPreviewPdf"
        type="no-data"
        title="暂不支持在线预览"
        description="Word 文档请下载后在本地查看"
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
        office-preview-url=""
        text-content=""
        :current-pdf-page="page"
        :pdf-page-count="totalPages && totalPages > 0 ? totalPages : undefined"
        :highlights="highlights"
        :active-highlight-id="activeHighlightId"
        :text-scroll-percent="0"
        @pdf-active-page="emit('update:page', $event)"
      />

      <div
        v-if="hoverVisible && hoverSegments.length"
        class="pdf-hover-pop"
        :style="hoverStyle"
      >
        <template v-for="(seg, i) in hoverSegments" :key="i">
          <strong v-if="seg.strong" class="pdf-hover-pop__match">{{ seg.text }}</strong>
          <template v-else>{{ seg.text }}</template>
        </template>
      </div>
    </div>
  </div>
<!-- OLD_TEMPLATE_BELOW
  <div class="pdf-viewer" :class="{ 'pdf-viewer-scanning': scanning }">
    <div class="pdf-viewer__body">

      <
      <
    </div>

    <div class="pdf-viewer__body">
      <EmptyState v-if="!fileUrl" type="no-data" title="请选择文档" />

      <EmptyState
        v-else-if="!canPreviewPdf"
        type="no-data"
        title="暂不支持在线预览"
        description="Word 文档请下载后在本地查看"
      />

      <PDF_Viewer
        v-else
        class="pdf-viewer__viewer"
        :class="{ 'pdf-viewer__viewer--no-original-label': hideOriginalLabel }"
        :node="node"
        :theme="theme"
        :is-pdf="true"
        :is-office="false"
        :is-image="false"
        :is-text="false"
        :file-url="fileUrl"
        :pdf-viewer-url="fileUrl"
        office-preview-url=""
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
-->
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'

import { PDF_Viewer } from '@angineer/docs-ui'
import '@angineer/docs-ui/style'
import { EmptyState } from '@shared/web'
import { normalizeRect } from '@shared/types'
import { useThemeStore } from '@shared/web/stores'
import * as pdfjsLib from 'pdfjs-dist'
import pdfjsWorker from 'pdfjs-dist/build/pdf.worker.min.mjs?url'
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

pdfjsLib.GlobalWorkerOptions.workerSrc = pdfjsWorker

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
    }
  }),
)

/* —— hover 取字浮框：PDF_Viewer 只渲染 canvas，无文字层，这里用 pdf.js 单独加载文本做命中 —— */
interface HoverTextItem {
  x: number
  y: number
  w: number
  h: number
  str: string
}

interface HoverPageData {
  items: HoverTextItem[]
  width: number
  height: number
}

let pdfDoc: pdfjsLib.PDFDocumentProxy | null = null
let pdfLoadingTask: pdfjsLib.PDFDocumentLoadingTask | null = null
const pageDataCache = new Map<number, Promise<HoverPageData>>()

interface HoverSegment {
  text: string
  strong: boolean
}

const hoverSegments = ref<HoverSegment[]>([])
const hoverVisible = ref(false)
const hoverPos = reactive({ x: 0, y: 0, flipY: false })
const hoverWidth = ref(0)

const hoverStyle = computed(() => ({
  left: `${hoverPos.x}px`,
  top: `${hoverPos.y}px`,
  width: `${hoverWidth.value}px`,
  transform: hoverPos.flipY ? 'translateY(8px)' : 'translateY(calc(-100% - 8px))',
}))

function disposePdfDoc(): void {
  pdfLoadingTask?.destroy().catch(() => {})
  pdfLoadingTask = null
  pdfDoc = null
  pageDataCache.clear()
}

watch(
  () => props.fileUrl,
  (url) => {
    disposePdfDoc()
    if (!url) return
    pdfLoadingTask = pdfjsLib.getDocument({ url })
    pdfLoadingTask.promise
      .then((doc) => {
        pdfDoc = doc
        // 文档加载完成信号：宿主据此在定位请求早于加载完成时重放跳页
        emit('loaded', url)
      })
      .catch(() => { pdfDoc = null })
  },
  { immediate: true },
)

onBeforeUnmount(disposePdfDoc)

async function loadPageData(page: number): Promise<HoverPageData> {
  const cached = pageDataCache.get(page)
  if (cached) return cached
  const promise = (async (): Promise<HoverPageData> => {
    const doc = pdfDoc
    if (!doc) return { items: [], width: 1, height: 1 }
    const pdfPage = await doc.getPage(page)
    const viewport = pdfPage.getViewport({ scale: 1 })
    const content = await pdfPage.getTextContent()
    const items: HoverTextItem[] = []
    for (const raw of content.items as Array<{ str?: string, transform?: number[], width?: number }>) {
      if (typeof raw.str !== 'string' || !raw.str || !raw.transform) continue
      const tx = pdfjsLib.Util.transform(viewport.transform, raw.transform)
      items.push({
        x: tx[4],
        y: tx[5],
        w: (raw.width ?? 0) * Math.abs(tx[0]),
        h: Math.abs(tx[3]) || Math.abs(tx[2]) || 1,
        str: raw.str,
      })
    }
    return { items, width: viewport.width, height: viewport.height }
  })()
  pageDataCache.set(page, promise)
  return promise
}

function onHoverOut(e: MouseEvent): void {
  const next = e.relatedTarget as HTMLElement | null
  if (!next?.closest?.('.pdf-highlight-box')) {
    hoverVisible.value = false
  }
}

function onHoverOver(e: MouseEvent): void {
  const box = (e.target as HTMLElement).closest?.('.pdf-highlight-box') as HTMLElement | null
  if (!box) {
    hoverVisible.value = false
    return
  }
  void showBoxPopup(box, e)
}

/** 只对“已高亮的证据 bbox”触发浮框：优先用后端原文片段（excerpt），缺失时用 pdf.js 按 bbox 取字兜底。 */
async function showBoxPopup(box: HTMLElement, e: MouseEvent): Promise<void> {
  // currentTarget 在事件派发结束后会置空，必须先缓存容器
  const bodyEl = e.currentTarget as HTMLElement
  const canvas = box.closest('.pdf-page-wrapper')?.querySelector('canvas[data-page]') as HTMLCanvasElement | null
  if (!canvas) return
  const page = Number(canvas.dataset.page)
  if (!page) return

  const canvasRect = canvas.getBoundingClientRect()
  const boxRect = box.getBoundingClientRect()
  if (canvasRect.width <= 0 || canvasRect.height <= 0) return
  const nx = (boxRect.left - canvasRect.left) / canvasRect.width
  const ny = (boxRect.top - canvasRect.top) / canvasRect.height
  const nw = boxRect.width / canvasRect.width
  const nh = boxRect.height / canvasRect.height

  const matched = matchHighlight(page, nx, ny, nw, nh)
  const excerpt = matched?.excerpt?.trim() ?? ''
  // 优先展示 bbox 内的全部内容；取不到时退回后端原文片段
  const fullText = await textInRect(page, nx, ny, nw, nh)
  const text = fullText || excerpt
  const segments = buildSegments(text, excerpt)
  if (!segments.length) {
    hoverVisible.value = false
    return
  }

  hoverSegments.value = segments
  const bodyRect = bodyEl.getBoundingClientRect()
  // 浮框与高亮 bbox 等宽、对齐 bbox 左侧，不再跟随鼠标位置
  const boxTop = boxRect.top - bodyRect.top
  const maxWidth = Math.max(180, bodyRect.width - 16)
  const width = Math.min(boxRect.width, maxWidth)
  const left = Math.max(8, Math.min(boxRect.left - bodyRect.left, bodyRect.width - 8 - width))
  hoverPos.x = left
  hoverPos.y = boxTop
  hoverWidth.value = width
  hoverPos.flipY = boxTop < 180
  hoverVisible.value = true
}

/** 把“雷同”匹配段在整段文字中加粗高亮；优先精确匹配，失败时忽略空白差异再匹配。 */
function buildSegments(fullText: string, excerpt: string): HoverSegment[] {
  if (!fullText) return []
  if (!excerpt) return [{ text: fullText, strong: false }]
  const plain: HoverSegment[] = [{ text: fullText, strong: false }]

  const exact = fullText.indexOf(excerpt)
  if (exact >= 0) {
    return splitAt(fullText, exact, exact + excerpt.length)
  }

  // 容错：压缩空白后匹配，再把偏移映射回原始文本
  const compactText = fullText.replace(/\s+/g, '')
  const compactNeedle = excerpt.replace(/\s+/g, '')
  const ci = compactText.indexOf(compactNeedle)
  if (ci < 0) return plain
  const start = mapCompactIndex(fullText, ci)
  const end = mapCompactIndex(fullText, ci + compactNeedle.length)
  if (start < 0 || end <= start) return plain
  return splitAt(fullText, start, end)
}

function splitAt(text: string, start: number, end: number): HoverSegment[] {
  const parts: HoverSegment[] = []
  if (start > 0) parts.push({ text: text.slice(0, start), strong: false })
  parts.push({ text: text.slice(start, end), strong: true })
  if (end < text.length) parts.push({ text: text.slice(end), strong: false })
  return parts.filter((p) => p.text.length > 0)
}

/** 压缩空白文本的下标 → 原始文本下标（忽略空白计数）。 */
function mapCompactIndex(text: string, compactIndex: number): number {
  let seen = 0
  for (let i = 0; i < text.length; i++) {
    if (!/\s/.test(text[i])) {
      if (seen === compactIndex) return i
      seen++
    }
  }
  return -1
}

/** 高亮框按面积排序渲染，DOM 顺序与 props.high 不一致；用归一化矩形中心最近匹配。 */
function matchHighlight(
  page: number,
  nx: number,
  ny: number,
  nw: number,
  nh: number,
): BlockRange | null {
  const cx = nx + nw / 2
  const cy = ny + nh / 2
  let best: BlockRange | null = null
  let bestDist = Infinity
  for (const h of props.high) {
    if (h.page !== page) continue
    const [x0, y0, x1, y1] = normalizeRect(h.bbox)
    const hcx = (x0 + x1) / 2
    const hcy = (y0 + y1) / 2
    const dist = (hcx - cx) ** 2 + (hcy - cy) ** 2
    if (dist < bestDist) {
      bestDist = dist
      best = h
    }
  }
  return best
}

/** 按归一化矩形取该区域内文字（pdf.js 兜底，excerpt 缺失时使用）。 */
async function textInRect(page: number, nx: number, ny: number, nw: number, nh: number): Promise<string> {
  const data = await loadPageData(page)
  if (!data.items.length) return ''
  const hit = data.items.filter((it) => {
    const ix = it.x / data.width
    const iy = it.y / data.height
    const iw = it.w / data.width
    const ih = it.h / data.height
    return ix < nx + nw && ix + iw > nx && iy < ny + nh && iy + ih > ny
  })
  if (!hit.length) return ''
  const sorted = [...hit].sort((a, b) => a.y - b.y || a.x - b.x)
  const lines: string[] = []
  let cur: HoverTextItem[] = []
  for (const it of sorted) {
    const last = cur[cur.length - 1]
    if (cur.length && last && Math.abs(it.y - last.y) > Math.max(it.h, last.h) * 0.7) {
      lines.push(cur.sort((a, b) => a.x - b.x).map((i) => i.str).join(''))
      cur = [it]
    } else {
      cur.push(it)
    }
  }
  if (cur.length) lines.push(cur.sort((a, b) => a.x - b.x).map((i) => i.str).join(''))
  return lines.join('\n').trim()
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.pdf-viewer {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-lg;
  overflow: hidden;
}

.pdf-viewer__bar {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-sm @spacing-md;
  border-bottom: 1px solid @divider-color;
  flex-shrink: 0;
}

.pdf-viewer__icon { color: @danger; }

.pdf-viewer__title {
  flex: 1;
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: @font-size-sm;
  color: @text-primary;
}

.pdf-viewer__tag {
  flex-shrink: 0;
  margin-inline-end: 0;
}

.pdf-viewer__body {
  flex: 1;
  min-height: 0;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  position: relative;
}

.pdf-viewer__viewer {
  flex: 1;
  min-height: 0;
}

.pdf-hover-pop {
  position: absolute;
  z-index: 30;
  max-height: 180px;
  overflow: auto;
  padding: @spacing-sm @spacing-md;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  background: @card-bg;
  box-shadow: @shadow-md;
  font-size: 13px;
  line-height: 1.6;
  color: @text-primary;
  white-space: pre-wrap;
  word-break: break-word;
  pointer-events: none;

  &__match {
    font-weight: 700;
    color: @danger;
    background: color-mix(in srgb, @danger 12%, transparent);
    border-radius: 2px;
    padding: 0 1px;
  }
}
</style>

<style lang="less">
/* 非 scoped：第三方 PDF_Viewer 内部元素 scoped 穿透不可靠，
   直接全局隐藏「原文」标签（.pane-title-prefix 仅该库使用，无副作用） */
.pane-title-prefix {
  display: none !important;
}

/* 修复 docs-ui 虚拟滚动 bug：.pdf-virtual-spacer 是 flex 容器子项，默认 flex-shrink
   会把内联 height（全文档高度）压缩到视口高度，导致大 PDF 只能滚到已渲染页、
   定位跳页失败。强制不收缩后 scrollHeight 恢复为全文高度，scrollToPdfPage 才能到达目标页。 */
.pdf-scroll-container .pdf-virtual-spacer {
  flex-shrink: 0 !important;
}
</style>
