<template>
  <div class="pdf-viewer" :class="{ 'pdf-viewer-scanning': scanning }">
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
import { computed } from 'vue'

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
}

.pdf-viewer__viewer {
  flex: 1;
  min-height: 0;
}
</style>

<style lang="less">
/* 非 scoped：第三方 PDF_Viewer 内部元素 scoped 穿透不可靠，
   直接全局隐藏「原文」标签（.pane-title-prefix 仅该库使用，无副作用） */
.pane-title-prefix {
  display: none !important;
}
</style>
