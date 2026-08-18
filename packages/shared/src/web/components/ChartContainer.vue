<template>
  <div ref="boxRef" class="chart-container" :style="{ height }">
    <div v-if="loading || !ready" class="chart-skeleton" aria-hidden="true">
      <div
        v-for="(h, i) in barHeights"
        :key="i"
        class="chart-skeleton-bar"
        :style="{ height: h, animationDelay: `${i * 0.12}s` }"
      />
    </div>
    <VChart v-else :option="option" autoresize class="chart" @click="onChartClick" />
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart, BarChart, PieChart } from 'echarts/charts'
import {
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent,
  DataZoomComponent,
} from 'echarts/components'
import VChart from 'vue-echarts'

defineProps<{
  option: Record<string, unknown>
  height?: string
  loading?: boolean
}>()

const emit = defineEmits<{ chartClick: [params: unknown] }>()
/** 容器有尺寸后再挂载 echarts，避免在折叠/未布局的容器里以 0 尺寸初始化产生告警。 */
const boxRef = ref<HTMLElement | null>(null)
const ready = ref(false)
let resizeObserver: ResizeObserver | null = null

function updateReady(): void {
  const el = boxRef.value
  if (!el) return
  ready.value = el.clientWidth > 0 && el.clientHeight > 0
}

onMounted(() => {
  updateReady()
  resizeObserver = new ResizeObserver(() => updateReady())
  if (boxRef.value) resizeObserver.observe(boxRef.value)
})

onBeforeUnmount(() => {
  resizeObserver?.disconnect()
  resizeObserver = null
})

function onChartClick(params: unknown): void {
  emit('chartClick', params)
}

use([
  CanvasRenderer,
  LineChart,
  BarChart,
  PieChart,
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent,
  DataZoomComponent,
])

const barHeights = ['42%', '68%', '55%', '82%', '60%', '74%', '48%', '64%']
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.chart-container {
  width: 100%;
  position: relative;
}

// 加载态：shimmer 柱状占位条，替代居中 spinner
.chart-skeleton {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: flex-end;
  gap: @spacing-sm;
  padding: @spacing-base @spacing-xl @spacing-xl;
  box-sizing: border-box;
}
.chart-skeleton-bar {
  flex: 1;
  border-radius: @radius-sm @radius-sm 0 0;
  background: @surface-hover;
  animation: chart-shimmer 1.8s ease-in-out infinite;
}
@keyframes chart-shimmer {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.35; }
}

.chart {
  width: 100%;
  height: 100%;
}
</style>
