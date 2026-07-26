<template>
  <SectionCard title="文档" nopad class="standard-reader">
    <template #extra>
      <div class="standard-reader__icons">
        <FileTextOutlined
          class="standard-reader__icon"
          :class="{ 'standard-reader__icon--on': renderMode === 'md' }"
          @click="renderMode = 'md'"
        />
        <FilePdfOutlined
          class="standard-reader__icon"
          :class="{ 'standard-reader__icon--on': renderMode === 'pdf' }"
          @click="renderMode = 'pdf'"
        />
      </div>
    </template>

    <DataSkeleton v-if="loading" />
    <EmptyState v-else-if="!doc" title="请选择左侧标准查看文档" />
    <EmptyState v-else-if="error" type="error" title="加载失败" :description="error" />

    <div v-else class="standard-reader__body">
      <!-- Markdown 模式 -->
      <div v-if="renderMode === 'md'" class="standard-reader__md">
        <div class="standard-reader__title">{{ doc.title }}</div>
        <div class="standard-reader__content" v-html="renderedMd" />
      </div>

      <!-- PDF 模式 -->
      <div v-else class="standard-reader__pdf">
        <div class="pdf-viewer">
          <div class="pdf-viewer__toolbar">
            <span>{{ doc.title }}</span>
            <span class="pdf-viewer__page">1 / 3</span>
          </div>
          <div class="pdf-viewer__pages">
            <div class="pdf-viewer__page-item">
              <div class="pdf-viewer__page-header">{{ doc.title }} — 1</div>
              <div class="pdf-viewer__page-body">
                <p>{{ doc.content.split('\n').slice(0, 20).join('\n') }}</p>
              </div>
            </div>
            <div class="pdf-viewer__page-item">
              <div class="pdf-viewer__page-header">{{ doc.title }} — 2</div>
              <div class="pdf-viewer__page-body">
                <p>{{ doc.content.split('\n').slice(20, 40).join('\n') }}</p>
              </div>
            </div>
            <div class="pdf-viewer__page-item">
              <div class="pdf-viewer__page-header">{{ doc.title }} — 3</div>
              <div class="pdf-viewer__page-body">
                <p>{{ doc.content.split('\n').slice(40).join('\n') }}</p>
              </div>
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
import SectionCard from '@shared/web/components/SectionCard.vue'
import DataSkeleton from '@shared/web/components/DataSkeleton.vue'
import EmptyState from '@shared/web/components/EmptyState.vue'
import type { StandardDocument } from '@/types'

const props = defineProps<{
  doc: StandardDocument | null
  loading: boolean
  error: string | null
}>()

const renderMode = ref<'md' | 'pdf'>('md')

const renderedMd = computed(() => {
  if (props.doc) {
    return props.doc.content
      .replace(/^### (.+)$/gm, '<h3>$1</h3>')
      .replace(/^## (.+)$/gm, '<h2>$1</h2>')
      .replace(/^# (.+)$/gm, '<h1>$1</h1>')
      .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
      .replace(/\n/g, '<br>')
  }
  return ''
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.standard-reader {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;

  :deep(.section-card-body) {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
    overflow: hidden;
  }

  &__icons {
    display: flex;
    gap: @spacing-sm;
  }

  &__icon {
    font-size: 18px;
    color: @text-tertiary;
    cursor: pointer;
    transition: color 0.2s;

    &--on {
      color: @brand-primary;
    }
  }

  &__body {
    flex: 1;
    min-height: 0;
    overflow-y: auto;
    padding: @spacing-xl;
  }

  &__title {
    font-size: @font-size-xl;
    font-weight: @font-weight-semibold;
    color: @text-primary;
    margin-bottom: @spacing-lg;
    padding-bottom: @spacing-lg;
    border-bottom: 1px solid @divider-color;
  }

  &__content {
    font-size: @font-size-base;
    color: @text-primary;
    line-height: 1.8;
  }

  &__md h1 { font-size: @font-size-2xl; margin-bottom: @spacing-md; }
  &__md h2 { font-size: @font-size-xl; margin: @spacing-xl 0 @spacing-md; }
  &__md h3 { font-size: @font-size-lg; margin: @spacing-lg 0 @spacing-sm; }
  &__md strong { font-weight: @font-weight-semibold; }
}

.pdf-viewer {
  max-width: 700px;
  margin: 0 auto;

  &__toolbar {
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

  &__page { color: @text-tertiary; }

  &__pages { display: flex; flex-direction: column; gap: 2px; }

  &__page-item {
    background: @card-bg;
    border: 1px solid @border-color;
    padding: @spacing-2xl @spacing-2xl @spacing-xl;
    box-shadow: @shadow-sm;
  }

  &__page-header {
    font-size: @font-size-xs;
    color: @text-tertiary;
    text-align: center;
    border-bottom: 1px solid @divider-color;
    padding-bottom: @spacing-sm;
    margin-bottom: @spacing-lg;
  }

  &__page-body {
    min-height: 300px;
    p { font-size: @font-size-base; line-height: 1.8; color: @text-primary; margin-bottom: @spacing-md; }
  }
}

@media (prefers-reduced-motion: reduce) {
  .standard-reader__icon { transition: none; }
}
</style>
