<template>
  <div class="charts-section">
    <div class="charts-header">
      <span class="charts-title">使用趋势</span>
      <a-radio-group :value="chartRange" button-style="solid" size="small" @change="emit('rangeChange', $event.target.value)">
        <a-radio-button value="7d">近7日</a-radio-button>
        <a-radio-button value="30d">近30日</a-radio-button>
        <a-radio-button value="month">本月</a-radio-button>
      </a-radio-group>
    </div>
    <div class="charts-grid">
      <SectionCard title="本功能使用频率（每日次数）">
        <ChartContainer :option="tasksOption" height="300px" :loading="loading" />
      </SectionCard>
      <SectionCard title="人员使用排序">
        <ChartContainer :option="usersOption" height="300px" :loading="loading" />
      </SectionCard>
      <SectionCard title="人声使用排序">
        <ChartContainer :option="voicesOption" height="300px" :loading="loading" />
      </SectionCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import ChartContainer from '@shared/web/components/ChartContainer.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import { useCssVar } from '@shared/web/composables/useCssVar'
import type { DubbingTask, DubbingUsageTimeSeries } from '@/types'

const props = defineProps<{ tasks: DubbingTask[]; timeSeries: DubbingUsageTimeSeries | null; loading: boolean }>()
const emit = defineEmits<{ rangeChange: [range: string] }>()

const chartRange = ref('30d')

const brandColor = useCssVar('--color-brand')
const accentColor = useCssVar('--color-accent')
const successColor = useCssVar('--color-success')

function chartTheme() {
  const isDark = document.documentElement.getAttribute('data-theme') === 'dark'
  return {
    axisColor: isDark ? '#52627A' : '#A8A29E',
    splitColor: isDark ? 'rgba(148, 163, 184, 0.08)' : 'rgba(0, 0, 0, 0.06)',
    tooltipBg: isDark ? 'rgba(15, 23, 42, 0.92)' : 'rgba(255, 255, 255, 0.92)',
    tooltipBorder: isDark ? 'rgba(148, 163, 184, 0.15)' : 'rgba(0, 0, 0, 0.06)',
    tooltipColor: isDark ? '#E2E8F0' : '#1C1917',
    legendColor: isDark ? '#94A3B8' : '#78716C',
  }
}

function makeBarGradient(hex: string) {
  return {
    type: 'linear' as const,
    x: 0, y: 0, x2: 0, y2: 1,
    colorStops: [
      { offset: 0, color: hex },
      { offset: 1, color: hex + '66' },
    ],
  }
}

const tasksOption = computed(() => {
  const t = chartTheme()
  const categories = props.timeSeries?.categories || []
  const data = props.timeSeries?.tasks?.[0]?.data || []
  if (!categories.length) return {}
  return {
    tooltip: { trigger: 'axis' as const, backgroundColor: t.tooltipBg, borderColor: t.tooltipBorder, borderWidth: 1, textStyle: { color: t.tooltipColor, fontSize: 13 } },
    grid: { left: 40, right: 16, bottom: 44, top: 16 },
    xAxis: { type: 'category' as const, data: categories, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11, rotate: 45 } },
    yAxis: { type: 'value' as const, min: 0, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11 }, splitLine: { lineStyle: { color: t.splitColor, type: 'dashed' as const } } },
    series: [{
      name: '每日使用次数',
      type: 'bar',
      data,
      barWidth: '42%',
      itemStyle: { color: makeBarGradient(brandColor.value), borderRadius: [6, 6, 0, 0] },
      animationDuration: 600,
      animationEasing: 'easeOutQuad' as const,
    }],
  }
})

function rankBarOption(counts: Map<string, number>, palette: string[]) {
  const t = chartTheme()
  const sorted = [...counts.entries()].sort((a, b) => b[1] - a[1]).slice(0, 10)
  const categories = sorted.map(e => e[0])
  const data = sorted.map((e, i) => ({
    value: e[1],
    itemStyle: { color: makeBarGradient(palette[i % palette.length]), borderRadius: [0, 6, 6, 0] },
  }))
  return {
    tooltip: { trigger: 'axis' as const, backgroundColor: t.tooltipBg, borderColor: t.tooltipBorder, borderWidth: 1, textStyle: { color: t.tooltipColor, fontSize: 13 } },
    grid: { left: 60, right: 32, bottom: 12, top: 16 },
    xAxis: { type: 'value' as const, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11 }, splitLine: { lineStyle: { color: t.splitColor, type: 'dashed' as const } } },
    yAxis: { type: 'category' as const, data: categories.slice().reverse(), axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11, interval: 0 } },
    series: [{ type: 'bar' as const, data: data.slice().reverse(), barWidth: '55%', animationDuration: 600, animationEasing: 'easeOutQuad' as const }],
  }
}

const usersOption = computed(() => {
  const counts = new Map<string, number>()
  for (const task of props.tasks) {
    const name = task.userName || '未知用户'
    counts.set(name, (counts.get(name) || 0) + 1)
  }
  return rankBarOption(counts, [brandColor.value, accentColor.value, successColor.value, brandColor.value, accentColor.value, successColor.value])
})

const voicesOption = computed(() => {
  const counts = new Map<string, number>()
  for (const task of props.tasks) {
    const name = task.voiceName || '未知音色'
    counts.set(name, (counts.get(name) || 0) + 1)
  }
  return rankBarOption(counts, [accentColor.value, brandColor.value, successColor.value, accentColor.value, brandColor.value, successColor.value])
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.charts-section {
  margin-top: @spacing-lg;
}
.charts-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: @spacing-md;
}
.charts-title {
  font-size: @font-size-lg;
  font-weight: @font-weight-semibold;
  color: @text-primary;
}
.charts-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: @spacing-lg;
}
.charts-grid :deep(.section-card-body) {
  padding: 10px @spacing-xl;
}

@media (max-width: 1200px) {
  .charts-grid { grid-template-columns: 1fr; }
}
</style>
