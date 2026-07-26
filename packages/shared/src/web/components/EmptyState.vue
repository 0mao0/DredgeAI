<template>
  <div class="empty-state">
    <svg class="empty-state__svg" viewBox="0 0 200 120" fill="none" aria-hidden="true">
      <!-- 海浪（双线） -->
      <path
        class="draw"
        pathLength="100"
        d="M10 94 Q 30 86 50 94 T 90 94 T 130 94 T 170 94 T 210 94"
        stroke="var(--color-text-tertiary)"
        stroke-width="2"
        stroke-linecap="round"
        opacity="0.5"
      />
      <path
        class="draw draw--d1"
        pathLength="100"
        d="M-10 104 Q 20 97 50 104 T 110 104 T 170 104 T 230 104"
        stroke="var(--color-text-tertiary)"
        stroke-width="2"
        stroke-linecap="round"
        opacity="0.3"
      />
      <!-- 疏浚船：船体 -->
      <path
        class="draw draw--d2"
        pathLength="100"
        d="M68 78 L132 78 L124 92 L76 92 Z"
        :stroke="accentColor"
        stroke-width="2"
        stroke-linejoin="round"
      />
      <!-- 驾驶室 -->
      <path
        class="draw draw--d3"
        pathLength="100"
        d="M92 78 L92 64 L114 64 L114 78"
        :stroke="accentColor"
        stroke-width="2"
        stroke-linejoin="round"
      />
      <!-- 烟囱 -->
      <path
        class="draw draw--d3"
        pathLength="100"
        d="M84 78 L84 69 L89 69 L89 78"
        :stroke="accentColor"
        stroke-width="2"
        stroke-linejoin="round"
      />
      <!-- 吊臂 + 吊索 + 抓斗 -->
      <path
        class="draw draw--d4"
        pathLength="100"
        d="M103 64 L130 46"
        :stroke="accentColor"
        stroke-width="2"
        stroke-linecap="round"
      />
      <path
        class="draw draw--d4"
        pathLength="100"
        d="M130 46 L130 60 M126 60 L134 60 L132 66 L128 66 Z"
        :stroke="accentColor"
        stroke-width="2"
        stroke-linejoin="round"
      />
      <!-- 类型符号（右上角） -->
      <g v-if="type === 'no-result'">
        <circle class="draw draw--d5" pathLength="100" cx="156" cy="40" r="10" :stroke="accentColor" stroke-width="2" />
        <path class="draw draw--d5" pathLength="100" d="M163 47 L170 54" :stroke="accentColor" stroke-width="2" stroke-linecap="round" />
      </g>
      <g v-else-if="type === 'error'">
        <path class="draw draw--d5" pathLength="100" d="M156 30 L166 50 L146 50 Z" :stroke="accentColor" stroke-width="2" stroke-linejoin="round" />
        <path class="draw draw--d5" pathLength="100" d="M156 38 L156 43 M156 46 L156 47" :stroke="accentColor" stroke-width="2" stroke-linecap="round" />
      </g>
      <g v-else-if="type === 'no-permission'">
        <rect class="draw draw--d5" pathLength="100" x="148" y="40" width="16" height="12" rx="2" :stroke="accentColor" stroke-width="2" />
        <path class="draw draw--d5" pathLength="100" d="M151 40 L151 36 Q 151 31 156 31 Q 161 31 161 36 L161 40" :stroke="accentColor" stroke-width="2" />
      </g>
    </svg>
    <div class="empty-state__title">{{ title || defaultTitle }}</div>
    <div v-if="description" class="empty-state__desc">{{ description }}</div>
    <div v-if="$slots.action" class="empty-state__action">
      <slot name="action" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

export type EmptyStateType = 'no-data' | 'no-result' | 'error' | 'no-permission'

const props = withDefaults(defineProps<{
  type?: EmptyStateType
  title?: string
  description?: string
}>(), {
  type: 'no-data',
})

const defaultTitles: Record<EmptyStateType, string> = {
  'no-data': '暂无数据',
  'no-result': '未找到匹配结果',
  'error': '加载失败',
  'no-permission': '暂无访问权限',
}

const defaultTitle = computed(() => defaultTitles[props.type])

const accentColor = computed(() =>
  props.type === 'error' ? 'var(--color-danger)' : 'var(--color-brand)',
)
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: @spacing-2xl @spacing-xl;
  text-align: center;
}

.empty-state__svg {
  width: 160px;
  height: auto;
  margin-bottom: @spacing-base;
}

// 入场：SVG 描边 draw-in 动画（pathLength 归一化为 100）
.draw {
  stroke-dasharray: 100;
  stroke-dashoffset: 100;
  animation: empty-draw-in 0.9s cubic-bezier(0.22, 1, 0.36, 1) forwards;
  &--d1 { animation-delay: 0.1s; }
  &--d2 { animation-delay: 0.2s; }
  &--d3 { animation-delay: 0.3s; }
  &--d4 { animation-delay: 0.4s; }
  &--d5 { animation-delay: 0.55s; }
}
@keyframes empty-draw-in {
  to { stroke-dashoffset: 0; }
}

.empty-state__title {
  font-size: @font-size-base;
  font-weight: @font-weight-medium;
  color: @text-primary;
}

.empty-state__desc {
  margin-top: @spacing-xs;
  font-size: @font-size-sm;
  color: @text-tertiary;
}

.empty-state__action {
  margin-top: @spacing-base;
}
</style>
