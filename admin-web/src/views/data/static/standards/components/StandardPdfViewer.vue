<template>
  <div class="standard-pdf-viewer">
    <EmptyState v-if="!fileUrl" type="no-data" title="暂无原文文件" />
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
      office-preview-url=""
      text-content=""
      :current-pdf-page="page"
      :highlights="viewerHighlights"
      :active-highlight-id="null"
      :text-scroll-percent="0"
      :show-side-panel-toggle="true"
      :side-panel-width="320"
      @pdf-active-page="onPageChange"
    >
      <template #side-panel>
        <div v-if="standard" class="standard-side-panel">
          <h4 class="standard-side-panel__title">{{ standard.name }}</h4>
          <dl class="standard-side-panel__meta">
            <div class="standard-side-panel__row">
              <dt>编号</dt>
              <dd>{{ standard.code }}</dd>
            </div>
            <div class="standard-side-panel__row">
              <dt>行业</dt>
              <dd>{{ standard.industry || '-' }}</dd>
            </div>
            <div class="standard-side-panel__row">
              <dt>性质</dt>
              <dd>{{ standard.nature || '-' }}</dd>
            </div>
            <div class="standard-side-panel__row">
              <dt>级别</dt>
              <dd>{{ standard.level || '-' }}</dd>
            </div>
            <div class="standard-side-panel__row">
              <dt>状态</dt>
              <dd>{{ standard.status || '-' }}</dd>
            </div>
            <div class="standard-side-panel__row">
              <dt>发布部门</dt>
              <dd>{{ standard.issuer || '-' }}</dd>
            </div>
            <div class="standard-side-panel__row">
              <dt>发布年份</dt>
              <dd>{{ standard.publishYear ?? '-' }}</dd>
            </div>
          </dl>
          <p v-if="standard.description" class="standard-side-panel__desc">{{ standard.description }}</p>
          <div v-if="highlights.length" class="standard-side-panel__parsed">
            <a-tag color="green">已解析</a-tag>
            <span>{{ highlights.length }} 个定位块</span>
          </div>
        </div>
      </template>
    </PDF_Viewer>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { PDF_Viewer } from '@angineer/docs-ui'
import '@angineer/docs-ui/style'
import { EmptyState } from '@shared/web'
import { useThemeStore } from '@shared/web/stores'
import type { StandardHighlight, StandardProperty } from '@/types'

interface ViewerHighlight extends StandardHighlight {
  hasRect: true
  lineStart: null
  lineEnd: null
  type: 'text'
}

const props = withDefaults(defineProps<{
  fileUrl: string
  page?: number
  highlights?: StandardHighlight[]
  standard?: StandardProperty | null
}>(), {
  page: 1,
  highlights: () => [],
  standard: null,
})

const emit = defineEmits<{
  'update:page': [value: number]
}>()

const themeStore = useThemeStore()
const theme = computed(() => themeStore.effectiveTheme)

const node = computed(() => ({
  status: 'completed',
  filePath: props.fileUrl,
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
@import '@shared/web/styles/variables.less';

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
  overflow-y: auto;
  padding: @spacing-md;
}
.standard-side-panel__title {
  margin: 0 0 @spacing-md;
  font-size: @font-size-base;
  font-weight: @font-weight-semibold;
  color: @text-primary;
}
.standard-side-panel__meta {
  margin: 0 0 @spacing-md;
}
.standard-side-panel__row {
  display: flex;
  gap: @spacing-sm;
  padding: 4px 0;
  font-size: @font-size-xs;
  dt {
    width: 64px;
    flex-shrink: 0;
    color: @text-tertiary;
  }
  dd {
    margin: 0;
    color: @text-secondary;
    word-break: break-all;
  }
}
.standard-side-panel__desc {
  margin: 0 0 @spacing-md;
  font-size: @font-size-xs;
  line-height: 1.7;
  color: @text-secondary;
}
.standard-side-panel__parsed {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  font-size: @font-size-xs;
  color: @text-secondary;
}
</style>
