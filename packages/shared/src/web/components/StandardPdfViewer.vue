<template>
  <div class="standard-pdf-viewer">
    <EmptyState v-if="!fileUrl" type="no-data" :title="emptyTitle" />
    <PDF_Viewer
      v-else
      class="standard-pdf-viewer__viewer"
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
      :highlights="viewerHighlights"
      :active-highlight-id="null"
      :text-scroll-percent="0"
      :show-side-panel-toggle="Boolean(parsedContent)"
      :side-panel-width="320"
      @pdf-active-page="onPageChange"
    >
      <template v-if="parsedContent" #side-panel>
        <div class="standard-side-panel">
          <PDFParsedViewerCombo
            v-model:active-tab="parsedTab"
            :markdown-content="parsedContent"
            :structured-items="emptyStructuredItems"
            :index-summary-stats="emptyIndexStats"
            :has-parsed-content="true"
            :content-scroll-percent="0"
            :active-linked-item-id="null"
            :active-line-range="null"
            :source-file-path="filePath"
            :graph-data="null"
            :dark="isDark"
          />
        </div>
      </template>
    </PDF_Viewer>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { PDF_Viewer, PDFParsedViewerCombo } from '@angineer/docs-ui'
import type { PreviewMode } from '@angineer/docs-ui'
import '@angineer/docs-ui/style'
import EmptyState from './EmptyState.vue'
import { useThemeStore } from '../stores'
import type { StandardHighlight } from '../../core/types/standard'

interface ViewerHighlight extends StandardHighlight {
  hasRect: true
  lineStart: null
  lineEnd: null
  type: 'text'
}

const props = withDefaults(defineProps<{
  fileUrl: string | null
  page?: number
  highlights?: StandardHighlight[]
  emptyTitle?: string
  /** 已解析的 Markdown 原文，用于在右侧展开解析视图 */
  parsedContent?: string
}>(), {
  page: 1,
  highlights: () => [],
  emptyTitle: '暂无原文文件',
  parsedContent: '',
})

const emit = defineEmits<{
  'update:page': [value: number]
}>()

const themeStore = useThemeStore()
const theme = computed(() => themeStore.effectiveTheme)
const isDark = computed(() => theme.value === 'dark')
const parsedTab = ref<PreviewMode>('Preview_Markdown')
const filePath = computed(() => props.fileUrl ?? '')

const emptyStructuredItems: never[] = []
const emptyIndexStats = {
  total: 0,
  paragraph: 0,
  title: 0,
  table: 0,
  formula: 0,
  figure: 0,
  headerFooter: 0,
  other: 0,
  maxLevel: 0,
}

const node = computed(() => ({
  status: 'completed',
  filePath: props.fileUrl ?? '',
}))

const viewerHighlights = computed<ViewerHighlight[]>(() =>
  props.highlights.map((h) => ({
    ...h,
    hasRect: true,
    lineStart: null,
    lineEnd: null,
    type: 'text',
  })),
)

function onPageChange(value: number): void {
  emit('update:page', value)
}
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.standard-pdf-viewer {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
  background: @content-bg;
  border: 1px solid @border-color;
  border-radius: @radius-lg;
  overflow: hidden;
}

.standard-pdf-viewer__viewer {
  flex: 1;
  min-height: 0;
}

.standard-side-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
</style>
