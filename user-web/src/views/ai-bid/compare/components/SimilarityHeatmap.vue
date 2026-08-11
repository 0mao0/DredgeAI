<template>
  <SectionCard title="相似度热力图" flush>
    <div class="heatmap">
      <a-segmented v-model:value="mode" :options="modeOptions" />
      <ChartContainer :option="option" height="300px" @chart-click="onCellClick" />
    </div>
  </SectionCard>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { use } from 'echarts/core'
import { HeatmapChart } from 'echarts/charts'
import { VisualMapComponent } from 'echarts/components'
import SectionCard from '@shared/web/components/SectionCard.vue'
import ChartContainer from '@shared/web/components/ChartContainer.vue'
import { useChartTheme } from '@shared/web/composables/useChartTheme'
import { useCssVar } from '@shared/web/composables/useCssVar'

const props = defineProps<{
  labels: string[]
  matrix: number[][]
  selfMatrix?: number[][]
}>()

const emit = defineEmits<{
  cellClick: [pair: { docA: string, docB: string }]
}>()

use([HeatmapChart, VisualMapComponent])

const mode = ref<'cross' | 'self'>('cross')
const modeOptions = [
  { label: '跨文档', value: 'cross' },
  { label: '文档内', value: 'self' },
]

const { chartTheme } = useChartTheme()
const brandColor = useCssVar('--color-brand')
const dangerColor = useCssVar('--color-danger')
const contentBg = useCssVar('--color-content-bg')

const activeMatrix = computed(() =>
  mode.value === 'self' && props.selfMatrix ? props.selfMatrix : props.matrix,
)

const option = computed(() => {
  const t = chartTheme()
  const data: [number, number, number][] = []
  activeMatrix.value.forEach((row, y) => {
    row.forEach((v, x) => data.push([x, y, v]))
  })
  return {
    grid: { left: 40, right: 16, bottom: 48, top: 12 },
    xAxis: {
      type: 'category',
      data: props.labels,
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: t.axisColor },
      splitArea: { show: true },
    },
    yAxis: {
      type: 'category',
      data: props.labels,
      inverse: true,
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: t.axisColor },
      splitArea: { show: true },
    },
    visualMap: {
      min: 0,
      max: 1,
      calculable: false,
      orient: 'horizontal',
      left: 'center',
      bottom: 0,
      itemHeight: 80,
      textStyle: { color: t.legendColor, fontSize: 11 },
      inRange: { color: [contentBg.value, brandColor.value, dangerColor.value] },
    },
    tooltip: {
      backgroundColor: t.tooltipBg,
      borderColor: t.tooltipBorder,
      textStyle: { color: t.tooltipColor },
      formatter: (p: unknown) => {
        const d = (p as { data?: [number, number, number] }).data
        if (!d) return ''
        return `${props.labels[d[1]]} ↔ ${props.labels[d[0]]}：${(d[2] * 100).toFixed(0)}%`
      },
    },
    series: [{
      type: 'heatmap',
      data,
      label: { show: false },
      emphasis: { itemStyle: { shadowBlur: 8 } },
      animationDuration: 600,
      animationEasing: 'easeOutQuad',
    }],
  }
})

function onCellClick(params: unknown): void {
  const d = (params as { data?: [number, number, number] }).data
  if (!d) return
  emit('cellClick', { docA: props.labels[d[0]], docB: props.labels[d[1]] })
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.heatmap {
  padding: @spacing-sm @spacing-xl @spacing-xl;

  .ant-segmented { margin-bottom: @spacing-md; }
}
</style>
