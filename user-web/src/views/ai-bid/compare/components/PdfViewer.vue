<template>
  <div
    class="pdf-viewer"
    :class="{
      'pdf-viewer-scanning': scanning,
      'pdf-viewer--hide-original': hideOriginalLabel,
    }"
  >
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
        :node="node"
        :theme="theme"
        :is-pdf="true"
        :is-office="false"
        :is-image="false"
        :is-text="false"
        :file-url="fileUrl"
        :pdf-viewer-url="fileUrl"
        :title="headerTitle"
        text-content=""
        :current-pdf-page="page"
        :pdf-page-range="pageRange?.length ? pageRange : undefined"
        :highlights="highlights"
        :active-highlight-id="activeHighlightId"
        :text-scroll-percent="0"
        :show-side-panel-toggle="showSidePanelToggle && hasSidePanelSlot"
        :side-panel-width="sidePanelWidth"
        :side-panel-open="sidePanelOpen"
        @update:side-panel-open="(v) => emit('update:sidePanelOpen', v)"
        @pdf-active-page="emit('update:page', $event)"
        @pdf-loaded="(url) => emit('loaded', url)"
        @select-highlight="(item) => emit('selectHighlight', item)"
      >
        <template v-if="hasSidePanelSlot" #side-panel>
          <slot name="side-panel" />
        </template>
      </PDF_Viewer>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, useSlots } from 'vue'

import { PDF_Viewer } from '@angineer/docs-ui'
import '@angineer/docs-ui/style'
import { EmptyState } from '@shared/web'
import { normalizeRect } from '@shared/types'
import { useThemeStore } from '@shared/web/stores'
import { isPdfFileName } from '../constants'
import type { BlockRange } from '@/types'

const props = withDefaults(defineProps<{
  fileUrl: string
  /** 文件原始名，用于在线预览类型判断（如 xx.pdf / xx.docx） */
  title?: string
  /** 显示在 PDF 组件左上角的标题，例如「招标文件」；不传则不显示 */
  headerTitle?: string
  page?: number
  high?: BlockRange[]
  scanning?: boolean
  activeHighlightId?: string | null
  /** 只渲染指定绝对页码（docs-ui pdf-page-range），空/不传 = 整篇 */
  pageRange?: number[]
  /** 隐藏引用库 PDF_Viewer 标题栏左侧的「原文」标签（库源码不可改，经 CSS 覆盖） */
  hideOriginalLabel?: boolean
  /** 是否显示右侧解析对比面板展开按钮（需要同时提供 #side-panel 插槽） */
  showSidePanelToggle?: boolean
  /** 右侧面板宽度，默认 400 */
  sidePanelWidth?: number
  /** 受控的右侧面板开关；不传则由 docs-ui 内部管理 */
  sidePanelOpen?: boolean
  /** 解析状态：parsing/processing 显示进度条，failed 显示错误，其余视为完成 */
  parseStatus?: string
  /** 当前解析阶段（如 structure/fts/vectors/graph） */
  parseStage?: string
  /** 当前解析阶段详情（显示在进度条右侧） */
  parseStep?: string
  /** 解析失败原因 */
  parseError?: string
}>(), {
  page: 1,
  high: () => [],
  scanning: false,
  activeHighlightId: null,
  hideOriginalLabel: false,
  showSidePanelToggle: false,
  sidePanelWidth: 400,
})

const emit = defineEmits<{
  'update:page': [value: number]
  'loaded': [url: string]
  'selectHighlight': [highlight: SelectHighlightEvent]
  'update:sidePanelOpen': [value: boolean]
}>()

const slots = useSlots()
const hasSidePanelSlot = computed(() => Boolean(slots['side-panel']))

interface SelectHighlightEvent {
  itemId?: string
  id?: string
}

interface ViewerNode {
  status: string
  filePath: string
  parseStage?: string
  parseStep?: string
  parseError?: string
}

function mapParseStatus(status?: string): string {
  if (!status) return 'completed'
  if (status === 'failed' || status === 'cancelled') return status
  if (['uploading', 'parsing', 'extracting', 'reviewing', 'pending', 'queued', 'processing'].includes(status)) {
    return 'processing'
  }
  return 'completed'
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
  status: mapParseStatus(props.parseStatus),
  filePath: props.fileUrl,
  parseStage: props.parseStage,
  parseStep: props.parseStep,
  parseError: props.parseError,
}))

/** bbox 为 0~1 归一化坐标，PDF_Viewer 高亮层按 0~1 比例直接渲染。 */
const highlights = computed<ViewerHighlight[]>(() =>
  props.high.map((h, i) => {
    const [x0, y0, x1, y1] = normalizeRect(h.bbox)
    return {
      id: `${h.pairId ?? h.docId}-${i}`,
      itemId: h.pairId ?? h.docId,
      page: h.page,
      hasRect: h.hasRect ?? true,
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

/* 按 prop 隐藏第三方 PDF_Viewer 标题栏左侧的「原文」标签（库源码不可改，经 :deep 覆盖） */
.pdf-viewer--hide-original {
  :deep(.pane-title-prefix) {
    display: none !important;
  }
}

/* 右侧解析面板内部高度撑满 */
.pdf-viewer {
  :deep(.pdf-viewer-side-panel) {
    min-height: 0;
  }
}
</style>
