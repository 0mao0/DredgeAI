<template>
  <SectionCard flush>
    <div class="heatmap">
      <ChartContainer :option="option" height="220px" @chart-click="onCellClick" />
      <div class="heatmap__title">相似度热力图</div>
    </div>
  </SectionCard>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { use } from 'echarts/core'
import { HeatmapChart } from 'echarts/charts'
import SectionCard from '@shared/web/components/SectionCard.vue'
import ChartContainer from '@shared/web/components/ChartContainer.vue'
import { useChartTheme } from '@shared/web/composables/useChartTheme'
import { useCssVar } from '@shared/web/composables/useCssVar'

const props = defineProps<{
  labels: string[]
  matrix: number[][]
}>()

const emit = defineEmits<{
  cellClick: [pair: { docA: string, docB: string }]
}>()

use([HeatmapChart])

const { chartTheme } = useChartTheme()
const successColor = useCssVar('--color-success')
const warningColor = useCssVar('--color-warning')
const dangerColor = useCssVar('--color-danger')
const borderColor = useCssVar('--color-border')

function heatmapValue(p: unknown): [number, number, number] | null {
  const raw = (p as { data?: unknown }).data
  const d = Array.isArray(raw) ? raw : (raw as { value?: unknown })?.value
  return Array.isArray(d) && d.length >= 3 ? (d as [number, number, number]) : null
}

const option = computed(() => {
  const t = chartTheme()
  const data: [number, number, number][] = []
  const diagonalData: [number, number, number][] = []
  props.matrix.forEach((row, y) => {
    row.forEach((v, x) => {
      if (x === y) diagonalData.push([x, y, v])
      else data.push([x, y, v])
    })
  })
  return {
    grid: { left: 40, right: 16, bottom: 8, top: 8 },
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
    tooltip: {
      backgroundColor: t.tooltipBg,
      borderColor: t.tooltipBorder,
      textStyle: { color: t.tooltipColor },
      formatter: (p: unknown) => {
        const d = heatmapValue(p)
        if (!d || d[0] === d[1]) return ''
        const [x, y, v] = d
        return `${props.labels[y]} ↔ ${props.labels[x]}：${(v * 100).toFixed(0)}%`
      },
    },
    series: [
      {
        type: 'heatmap',
        data,
        itemStyle: {
          color: (p: unknown) => {
            const d = heatmapValue(p)
            if (!d || d[0] === d[1]) return 'transparent'
            const v = d[2]
            if (v >= 0.5) return dangerColor.value
            if (v >= 0.2) return warningColor.value
            return successColor.value
          },
        },
        label: {
          show: true,
          formatter: (p: unknown) => {
            const d = heatmapValue(p)
            if (!d || d[0] === d[1]) return ''
            const pct = Math.round(d[2] * 100)
            if (pct === 0) return '不相似'
            if (pct <= 20) return `低度相似 ${pct}%`
            if (pct <= 40) return `中度相似 ${pct}%`
            return `高度相似 ${pct}%`
          },
          color: t.axisColor,
          fontSize: 10,
        },
        emphasis: { itemStyle: { shadowBlur: 8 } },
        animationDuration: 600,
        animationEasing: 'easeOutQuad',
      },
      {
        type: 'heatmap',
        data: diagonalData,
        itemStyle: { color: borderColor.value },
        label: { show: false },
        tooltip: { show: false },
        emphasis: { disabled: true },
      },
    ],
  }
})

function onCellClick(params: unknown): void {
  const d = heatmapValue(params)
  if (!d || d[0] === d[1]) return
  emit('cellClick', { docA: props.labels[d[0]], docB: props.labels[d[1]] })
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.heatmap {
  padding: @spacing-xs @spacing-lg 0;

  &__title {
    margin-top: 0;
    font-size: @font-size-xs;
    color: @text-tertiary;
    text-align: center;
  }
}
</style>
