<template>
  <SectionCard :title="cardTitle" class="doc-viewer">
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

    <DataSkeleton v-if="loading" :rows="5" />

    <EmptyState v-else-if="!doc" type="no-data" :title="emptyTitle" />

    <EmptyState v-else-if="error" type="error" :title="error" />

    <div v-else class="doc-viewer__body">
      <div v-if="renderMode === 'md'" class="doc-viewer__md">
        <div class="doc-viewer__title">{{ doc.title }}</div>
        <div class="doc-viewer__content" v-html="renderedMd" />
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
import { ref, computed } from 'vue'
import { FileTextOutlined, FilePdfOutlined } from '@ant-design/icons-vue'
import SectionCard from './SectionCard.vue'
import DataSkeleton from './DataSkeleton.vue'
import EmptyState from './EmptyState.vue'

export interface DocContent {
  title: string
  content: string
}

const props = withDefaults(defineProps<{
  doc: DocContent | null
  loading?: boolean
  error?: string | null
  cardTitle?: string
  emptyTitle?: string
}>(), {
  cardTitle: '文档',
  emptyTitle: '暂无文档',
})

const renderMode = ref<'md' | 'pdf'>('md')

const renderedMd = computed(() => {
  const d = props.doc
  if (!d) return ''
  return d.content
    .replace(/^### (.+)$/gm, '<h3>$1</h3>')
    .replace(/^## (.+)$/gm, '<h2>$1</h2>')
    .replace(/^# (.+)$/gm, '<h1>$1</h1>')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    .replace(/\n/g, '<br>')
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

  h1 { font-size: @font-size-2xl; margin-bottom: @spacing-md; }
  h2 { font-size: @font-size-xl; margin: @spacing-xl 0 @spacing-md; }
  h3 { font-size: @font-size-lg; margin: @spacing-lg 0 @spacing-sm; }
  strong { font-weight: @font-weight-semibold; }
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
</style>
