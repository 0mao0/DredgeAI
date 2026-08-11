<template>
  <div class="pdf-viewer">
    <div class="pdf-viewer__toolbar">
      <FilePdfOutlined class="pdf-viewer__icon" />
      <span class="pdf-viewer__title" :title="title">{{ title || '未选择文档' }}</span>
      <a-tag class="pdf-viewer__tag">占位渲染</a-tag>
      <span v-if="totalPages" class="pdf-viewer__page">第 {{ page }} / {{ totalPages }} 页</span>
    </div>

    <div class="pdf-viewer__body">
      <EmptyState v-if="!src" type="no-data" title="请选择文档" />

      <div v-else class="pdf-viewer__page-wrap" :style="{ width: `${(zoom ?? 1) * 100}%` }">
        <div class="pdf-viewer__paper">
          <div class="pdf-viewer__lines">
            <div v-for="i in 24" :key="i" class="pdf-viewer__line" :class="{ 'pdf-viewer__line--short': i % 5 === 0 }" />
          </div>
          <div
            v-for="(h, i) in rects"
            :key="i"
            class="pdf-viewer__rect"
            :class="{ 'pdf-viewer__rect--active': h.pairId }"
            :style="h.style"
            :title="h.excerpt"
          >
            <span v-if="h.excerpt" class="pdf-viewer__excerpt" :style="{ color: h.color }">{{ h.excerpt }}</span>
          </div>
        </div>
        <div class="pdf-viewer__placeholder-note">PDF 真渲染（pdf.js）接入中，当前为 bbox 示意</div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { FilePdfOutlined } from '@ant-design/icons-vue'
import EmptyState from '@shared/web/components/EmptyState.vue'
import { normalizeRect } from '@shared/types'
import type { BlockRange } from '@/types'

const props = withDefaults(defineProps<{
  src?: string
  title?: string
  page?: number
  totalPages?: number
  high?: BlockRange[]
  zoom?: number
}>(), {
  page: 1,
  high: () => [],
})

const PAIR_COLORS = ['#EF4444', '#F59E0B', '#3B82F6', '#10B981']

function pairColor(pairId?: string): string {
  if (!pairId) return '#8C8C8C'
  let hash = 0
  for (let i = 0; i < pairId.length; i++) hash += pairId.charCodeAt(i)
  return PAIR_COLORS[hash % PAIR_COLORS.length]
}

const rects = computed(() =>
  props.high.map((h) => {
    const [x0, y0, x1, y1] = normalizeRect(h.bbox)
    const color = pairColor(h.pairId)
    return {
      pairId: h.pairId,
      excerpt: h.excerpt,
      color,
      style: {
        left: `${x0 * 100}%`,
        top: `${y0 * 100}%`,
        width: `${(x1 - x0) * 100}%`,
        height: `${(y1 - y0) * 100}%`,
        borderColor: color,
        background: `color-mix(in srgb, ${color} 12%, transparent)`,
      },
    }
  }),
)
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.pdf-viewer {
  height: 100%;
  display: flex;
  flex-direction: column;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-lg;
  overflow: hidden;
}

.pdf-viewer__toolbar {
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

.pdf-viewer__tag { flex-shrink: 0; margin-inline-end: 0; }

.pdf-viewer__page {
  font-size: @font-size-xs;
  color: @text-tertiary;
  flex-shrink: 0;
  font-variant-numeric: tabular-nums;
}

.pdf-viewer__body {
  flex: 1;
  min-height: 0;
  overflow: auto;
  padding: @spacing-base;
  background: @content-bg;
}

.pdf-viewer__page-wrap {
  max-width: 720px;
  margin: 0 auto;
}

.pdf-viewer__paper {
  position: relative;
  width: 100%;
  aspect-ratio: 210 / 297;
  background: @card-bg;
  border: 1px solid @border-color;
  box-shadow: @shadow-sm;
  overflow: hidden;
}

.pdf-viewer__lines {
  position: absolute;
  inset: 6% 7%;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.pdf-viewer__line {
  height: 6px;
  border-radius: 3px;
  background: @surface-hover;

  &--short { width: 62%; }
}

.pdf-viewer__rect {
  position: absolute;
  border: 1.5px solid;
  border-radius: 2px;
  pointer-events: auto;
}

.pdf-viewer__excerpt {
  position: absolute;
  left: 0;
  top: 100%;
  font-size: 11px;
  line-height: 1.4;
  white-space: nowrap;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  background: @card-bg;
  padding: 0 3px;
  border-radius: 2px;
}

.pdf-viewer__placeholder-note {
  text-align: center;
  font-size: @font-size-xs;
  color: @text-tertiary;
  padding: @spacing-sm 0 0;
}
</style>
