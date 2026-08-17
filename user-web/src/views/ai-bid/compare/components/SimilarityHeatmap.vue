<template>
  <SectionCard nopad>
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
import { VisualMapComponent } from 'echarts/components'
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

use([HeatmapChart, VisualMapComponent])

const { chartTheme } = useChartTheme()
const successColor = useCssVar('--color-success')
const warningColor = useCssVar('--color-warning')
const dangerColor = useCssVar('--color-danger')
const borderColor = useCssVar('--color-border')
const textColor = useCssVar('--color-text-secondary')

const BADGE_LETTERS = ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'] as const

function badgeToken(label: string): string {
  const letter = (label || '').trim().charAt(0).toUpperCase()
  return (BADGE_LETTERS as readonly string[]).includes(letter) ? letter : 'A'
}

function badgeFormatter(value: string): string {
  const token = badgeToken(value)
  return `{spine${token}| }{cover${token}|${value}}`
}

function badgeRich(): Record<string, unknown> {
  const border = borderColor.value || '#A8A29E'
  const text = textColor.value || '#78716C'
  return Object.fromEntries(BADGE_LETTERS.flatMap((l) => [
    [`spine${l}`, {
      backgroundColor: 'transparent',
      borderColor: border,
      borderWidth: 1,
      width: 3,
      height: 20,
      borderRadius: [2, 0, 0, 2],
    }],
    [`cover${l}`, {
      backgroundColor: 'transparent',
      borderColor: border,
      borderWidth: 1,
      color: text,
      padding: [2, 5],
      fontSize: 12,
      fontWeight: 600,
      align: 'center',
      verticalAlign: 'middle',
      height: 20,
      lineHeight: 16,
      borderRadius: [0, 4, 4, 0],
    }],
  ]))
}

function heatmapValue(p: unknown): [number, number, number] | null {
  const raw = (p as { data?: unknown }).data
  const d = Array.isArray(raw) ? raw : (raw as { value?: unknown })?.value
  return Array.isArray(d) && d.length >= 3 ? (d as [number, number, number]) : null
}

function cellColor(v: number): string {
  if (v >= 0.5) return dangerColor.value || '#EF4444'
  if (v >= 0.2) return warningColor.value || '#F59E0B'
  return successColor.value || '#10B981'
}

const option = computed(() => {
  const t = chartTheme()
  const data: { value: [number, number, number], itemStyle: { color: string } }[] = []
  props.matrix.forEach((row, y) => {
    row.forEach((v, x) => {
      data.push({
        value: [x, y, v],
        itemStyle: { color: x === y ? 'transparent' : cellColor(v) },
      })
    })
  })
  return {
    grid: { left: 40, right: 16, bottom: 8, top: 8 },
    visualMap: {
      show: false,
      min: 0,
      max: 1,
      seriesIndex: 0,
      inRange: { color: ['#10B981', '#F59E0B', '#EF4444'] },
    },
    xAxis: {
      type: 'category',
      data: props.labels,
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: {
        color: t.axisColor,
        formatter: badgeFormatter,
        rich: badgeRich(),
      },
      splitArea: { show: true },
    },
    yAxis: {
      type: 'category',
      data: props.labels,
      inverse: true,
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: {
        color: t.axisColor,
        formatter: badgeFormatter,
        rich: badgeRich(),
      },
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
        label: {
          show: true,
          formatter: (p: unknown) => {
            const d = heatmapValue(p)
            if (!d || d[0] === d[1]) return ''
            const pct = Math.round(d[2] * 100)
            if (pct === 0) return '不相似 0%'
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
  padding: @spacing-xs @spacing-lg @spacing-sm;

  &__title {
    margin-top: 0;
    font-size: @font-size-xs;
    color: @text-tertiary;
    text-align: center;
  }
}
</style>
