<template>
  <SectionCard :title="cardTitle" class="doc-viewer" :nopad="!!steps">
    <template #extra>
      <div class="doc-viewer__icons">
        <FileTextOutlined
          class="doc-viewer__icon"
          :class="{ 'doc-viewer__icon--on': renderMode === 'md' }"
          @click="renderMode = 'md'"
        />
        <FilePdfOutlined
          class="doc-viewer__icon"
          :class="{ 'doc-viewer__icon--on': renderMode === 'pdf' }"
          @click="renderMode = 'pdf'"
        />
        <slot name="extra" />
      </div>
    </template>

    <div v-if="steps" class="doc-progress">
      <div
        v-for="(s, i) in steps"
        :key="i"
        class="doc-progress__step"
        :class="stepClass(s)"
      >
        <span class="doc-progress__dot" />
        <span class="doc-progress__label">{{ s.title }}</span>
        <span v-if="s.progress != null" class="doc-progress__pct">{{ s.progress }}%</span>
        <span v-if="i < steps.length - 1" class="doc-progress__line" />
      </div>
    </div>

    <DataSkeleton v-if="loading" :rows="5" />

    <EmptyState v-else-if="!doc" type="no-data" :title="emptyTitle" />

    <EmptyState v-else-if="error" type="error" :title="error" />

    <div v-else class="doc-viewer__body" :class="{ 'doc-viewer__body--tight': !!steps }">
      <div v-if="renderMode === 'md'" class="doc-viewer__md">
        <div v-if="pagedMode" class="md-toolbar">
          <a-dropdown v-if="outline.length" trigger="click">
            <a-button type="text" size="small" title="目录">
              <UnorderedListOutlined />
            </a-button>
            <template #overlay>
              <div class="outline-panel">
                <div
                  v-for="(o, i) in outline"
                  :key="i"
                  class="outline-item"
                  :class="[`outline-item--lv${o.level}`, { 'outline-item--active': o.page === currentPage }]"
                  @click="jumpToPage(o.page)"
                >
                  {{ o.title }}
                </div>
              </div>
            </template>
          </a-dropdown>

          <div class="md-toolbar__group">
            <a-button type="text" size="small" :disabled="scale <= 0.5" @click="zoomOut">
              <MinusOutlined />
            </a-button>
            <span class="md-toolbar__zoom" title="重置缩放" @click="scale = 1">{{ Math.round(scale * 100) }}%</span>
            <a-button type="text" size="small" :disabled="scale >= 2" @click="zoomIn">
              <PlusOutlined />
            </a-button>
          </div>

          <div class="md-toolbar__group">
            <a-button type="text" size="small" :disabled="currentPage <= 1" @click="jumpToPage(currentPage - 1)">
              <LeftOutlined />
            </a-button>
            <span class="md-toolbar__page">{{ currentPage }} / {{ pageCount }}</span>
            <a-button type="text" size="small" :disabled="currentPage >= pageCount" @click="jumpToPage(currentPage + 1)">
              <RightOutlined />
            </a-button>
          </div>

          <div class="md-toolbar__group md-toolbar__search">
            <a-input
              v-model:value="searchText"
              size="small"
              placeholder="搜索本页"
              allow-clear
              class="md-toolbar__search-input"
              @press-enter="nextMatch"
            />
            <template v-if="searchText">
              <span class="md-toolbar__matches">{{ matchCount ? currentMatch + 1 : 0 }}/{{ matchCount }}</span>
              <a-button type="text" size="small" :disabled="!matchCount" @click="prevMatch">
                <ArrowUpOutlined />
              </a-button>
              <a-button type="text" size="small" :disabled="!matchCount" @click="nextMatch">
                <ArrowDownOutlined />
              </a-button>
            </template>
          </div>
        </div>

        <div class="doc-viewer__title">{{ doc.title }}</div>
        <div
          ref="contentRef"
          class="doc-viewer__content"
          :style="{ fontSize: `${14 * scale}px` }"
          v-html="renderedMd"
        />
      </div>

      <div v-else class="doc-viewer__pdf">
        <div class="pdf-toolbar">
          <span>{{ doc.title }}</span>
          <span class="pdf-toolbar__page">1 / 3</span>
        </div>
        <div class="pdf-pages">
          <div class="pdf-page">
            <div class="pdf-page__header">{{ doc.title }} — 1</div>
            <div class="pdf-page__body">
              <p>{{ doc.content.split('\n').slice(0, 20).join('\n') }}</p>
            </div>
          </div>
          <div class="pdf-page">
            <div class="pdf-page__header">{{ doc.title }} — 2</div>
            <div class="pdf-page__body">
              <p>{{ doc.content.split('\n').slice(20, 40).join('\n') }}</p>
            </div>
          </div>
          <div class="pdf-page">
            <div class="pdf-page__header">{{ doc.title }} — 3</div>
            <div class="pdf-page__body">
              <p>{{ doc.content.split('\n').slice(40).join('\n') }}</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  </SectionCard>
</template>

<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'
import {
  FileTextOutlined,
  FilePdfOutlined,
  UnorderedListOutlined,
  MinusOutlined,
  PlusOutlined,
  LeftOutlined,
  RightOutlined,
  ArrowUpOutlined,
  ArrowDownOutlined,
} from '@ant-design/icons-vue'
import SectionCard from './SectionCard.vue'
import DataSkeleton from './DataSkeleton.vue'
import EmptyState from './EmptyState.vue'
import { extractMath, restoreMath } from './katex'

export interface DocContent {
  title: string
  content: string
  pages?: string[]
}

export interface DocProgressStep {
  title: string
  status: 'wait' | 'process' | 'finish' | 'error'
  progress?: number
}

interface OutlineEntry {
  title: string
  level: number
  page: number
}

const props = withDefaults(defineProps<{
  doc: DocContent | null
  steps?: DocProgressStep[]
  loading?: boolean
  error?: string | null
  cardTitle?: string
  emptyTitle?: string
}>(), {
  cardTitle: '文档',
  emptyTitle: '暂无文档',
})

function stepClass(s: DocProgressStep): Record<string, boolean> {
  return {
    'doc-progress__step--done': s.status === 'finish',
    'doc-progress__step--active': s.status === 'process',
    'doc-progress__step--error': s.status === 'error',
  }
}

const renderMode = ref<'md' | 'pdf'>('md')

const currentPage = ref(1)
const scale = ref(1)
const searchText = ref('')
const currentMatch = ref(0)
const contentRef = ref<HTMLElement>()

const pagedMode = computed(() => (props.doc?.pages?.length ?? 0) > 0)
const pageCount = computed(() => (pagedMode.value ? props.doc?.pages?.length ?? 1 : 1))

const currentPageSource = computed(() => {
  const d = props.doc
  if (!d) return ''
  if (!pagedMode.value) return d.content
  return d.pages?.[currentPage.value - 1] ?? ''
})

const outline = computed<OutlineEntry[]>(() => {
  const pages = props.doc?.pages
  if (!pages?.length) return []
  const entries: OutlineEntry[] = []
  pages.forEach((pageContent, idx) => {
    for (const m of pageContent.matchAll(/^(#{1,3})\s+(.+)$/gm)) {
      entries.push({ title: m[2], level: m[1].length, page: idx + 1 })
    }
  })
  return entries
})

function jumpToPage(page: number): void {
  currentPage.value = Math.min(Math.max(page, 1), pageCount.value)
}

function zoomOut(): void {
  scale.value = Math.max(0.5, Math.round((scale.value - 0.1) * 100) / 100)
}

function zoomIn(): void {
  scale.value = Math.min(2, Math.round((scale.value + 0.1) * 100) / 100)
}

function escapeRegExp(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

const matchCount = computed(() => {
  const kw = searchText.value
  if (!kw) return 0
  return currentPageSource.value.split(kw).length - 1
})

function nextMatch(): void {
  if (!matchCount.value) return
  currentMatch.value = (currentMatch.value + 1) % matchCount.value
}

function prevMatch(): void {
  if (!matchCount.value) return
  currentMatch.value = (currentMatch.value - 1 + matchCount.value) % matchCount.value
}

const renderedMd = computed(() => {
  const source = currentPageSource.value
  if (!source) return ''
  const { text, segments } = extractMath(source)
  let html = text
    .replace(/^### (.+)$/gm, '<h3>$1</h3>')
    .replace(/^## (.+)$/gm, '<h2>$1</h2>')
    .replace(/^# (.+)$/gm, '<h1>$1</h1>')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\n/g, '<br>')
  html = restoreMath(html, segments)
  const kw = searchText.value
  if (kw) {
    let n = 0
    html = html.replace(new RegExp(escapeRegExp(kw), 'g'), (m: string) => {
      const cls = n === currentMatch.value ? ' class="is-current"' : ''
      n += 1
      return `<mark${cls}>${m}</mark>`
    })
  }
  return html
})

watch([searchText, currentPageSource], () => {
  currentMatch.value = 0
})

watch(renderedMd, () => {
  if (!searchText.value) return
  nextTick(() => {
    contentRef.value?.querySelector('mark.is-current')?.scrollIntoView({ block: 'center' })
  })
})
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.doc-viewer {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;

  :deep(.section-card-body) {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }
}

.doc-viewer__body {
  flex: 1;
  min-height: 0;
  overflow-y: auto;
  padding: @spacing-xl;
  &--tight { padding-top: @spacing-sm; }
}

.doc-viewer__title {
  font-size: @font-size-xl;
  font-weight: @font-weight-semibold;
  color: @text-primary;
  margin-bottom: @spacing-lg;
  padding-bottom: @spacing-lg;
  border-bottom: 1px solid @divider-color;
}

.doc-viewer__content {
  font-size: @font-size-base;
  color: @text-primary;
  line-height: 1.8;

  :deep(h1) { font-size: 1.6em; margin-bottom: @spacing-md; }
  :deep(h2) { font-size: 1.3em; margin: @spacing-xl 0 @spacing-md; }
  :deep(h3) { font-size: 1.15em; margin: @spacing-lg 0 @spacing-sm; }
  :deep(strong) { font-weight: @font-weight-semibold; }
  :deep(mark) {
    background: color-mix(in srgb, @warning 30%, transparent);
    color: inherit;
    padding: 0 1px;
    border-radius: 2px;
  }
  :deep(mark.is-current) {
    background: @warning;
    color: #fff;
  }
  :deep(.katex-error) {
    color: @danger;
    background: color-mix(in srgb, @danger 8%, transparent);
    padding: 0 4px;
    border-radius: @radius-sm;
    font-size: 0.9em;
  }
  :deep(.katex-display) { margin: @spacing-sm 0; }
}

.doc-viewer__icons {
  display: flex;
  gap: @spacing-sm;
}
.doc-viewer__icon {
  font-size: 18px;
  color: @text-tertiary;
  cursor: pointer;
  transition: color @transition-fast;
  &--on { color: @brand-primary; }
}

.md-toolbar {
  display: flex;
  align-items: center;
  gap: @spacing-md;
  flex-wrap: wrap;
  padding: @spacing-xs @spacing-sm;
  margin-bottom: @spacing-md;
  background: @content-bg;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  position: sticky;
  top: -@spacing-xl;
  z-index: 5;
}
.md-toolbar__group {
  display: flex;
  align-items: center;
  gap: 2px;
}
.md-toolbar__zoom {
  min-width: 42px;
  text-align: center;
  font-size: @font-size-xs;
  color: @text-secondary;
  cursor: pointer;
  font-variant-numeric: tabular-nums;
}
.md-toolbar__page {
  min-width: 48px;
  text-align: center;
  font-size: @font-size-xs;
  color: @text-secondary;
  font-variant-numeric: tabular-nums;
}
.md-toolbar__search { margin-left: auto; }
.md-toolbar__search-input { width: 140px; }
.md-toolbar__matches {
  font-size: @font-size-xs;
  color: @text-tertiary;
  min-width: 36px;
  text-align: center;
  font-variant-numeric: tabular-nums;
}

.outline-panel {
  max-height: 320px;
  overflow-y: auto;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  box-shadow: @shadow-md;
  padding: @spacing-xs 0;
  min-width: 220px;
}
.outline-item {
  padding: 6px @spacing-md;
  font-size: @font-size-sm;
  color: @text-secondary;
  cursor: pointer;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  &:hover { background: @surface-hover; color: @text-primary; }
  &--lv2 { padding-left: @spacing-xl; }
  &--lv3 { padding-left: @spacing-2xl; }
  &--active { color: @brand-primary; }
}

/* 外部 API 进度条（附在 PDF 上方，极窄融合） */
.doc-progress {
  display: flex;
  align-items: center;
  padding: 0 @spacing-xl;
  flex-shrink: 0;
  height: 16px;
  box-sizing: border-box;
  background: @content-bg;
}
.doc-progress__step {
  display: flex;
  align-items: center;
  gap: 3px;
  flex: 1;
  font-size: 11px;
  color: @text-tertiary;
  position: relative;
  white-space: nowrap;
  &--done { color: @success; }
  &--active { color: @brand-primary; }
  &--error { color: @danger; }
}
.doc-progress__dot {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: currentColor;
  flex-shrink: 0;
}
.doc-progress__label {
  overflow: hidden;
  text-overflow: ellipsis;
}
.doc-progress__pct {
  font-variant-numeric: tabular-nums;
}
.doc-progress__line {
  flex: 1;
  height: 1px;
  background: currentColor;
  opacity: 0.2;
  margin: 0 6px;
  .doc-progress__step--active & { opacity: 0.5; }
  .doc-progress__step--done & { opacity: 0.4; }
}

/* PDF 模拟 */
.pdf-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: @spacing-sm @spacing-md;
  background: @content-bg;
  border-radius: @radius-base @radius-base 0 0;
  border: 1px solid @border-color;
  border-bottom: none;
  font-size: @font-size-sm;
  color: @text-secondary;
}
.pdf-toolbar__page { color: @text-tertiary; }

.pdf-pages { display: flex; flex-direction: column; gap: 2px; }
.pdf-page {
  background: @card-bg;
  border: 1px solid @border-color;
  padding: @spacing-2xl @spacing-2xl @spacing-xl;
  box-shadow: @shadow-sm;
}
.pdf-page__header {
  font-size: @font-size-xs;
  color: @text-tertiary;
  text-align: center;
  border-bottom: 1px solid @divider-color;
  padding-bottom: @spacing-sm;
  margin-bottom: @spacing-lg;
}
.pdf-page__body {
  min-height: 300px;
  p { font-size: @font-size-base; line-height: 1.8; color: @text-primary; margin-bottom: @spacing-md; }
}

@media (prefers-reduced-motion: reduce) {
  .doc-viewer__icon { transition: none; }
}
</style>
