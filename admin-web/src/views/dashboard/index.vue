<template>
  <div class="page-container">
    <PageHeader title="仪表盘" description="系统运营概览">
      <template #extra>
        <AppButton size="sm" @click="refresh">刷新数据</AppButton>
      </template>
    </PageHeader>

    <a-result
      v-if="error && metrics.length === 0"
      status="error"
      title="数据加载失败"
      :sub-title="error.message"
    >
      <template #extra>
        <AppButton variant="primary" @click="refresh">重新加载</AppButton>
      </template>
    </a-result>

    <template v-else>
      <a-row :gutter="[16, 16]" class="metrics-row">
        <a-col v-for="(m, i) in metrics" :key="m.id" :xs="24" :sm="12" :lg="6" :style="itemStyle(i)">
          <MetricCard v-bind="m" />
        </a-col>
      </a-row>

      <a-row :gutter="[16, 16]" class="charts-row">
        <a-col :xs="24" :lg="16">
          <SectionCard title="API 调用趋势" flush>
            <ChartContainer :option="apiCallsChartOption" height="300px" :loading="loading" />
          </SectionCard>
        </a-col>
        <a-col :xs="24" :lg="8">
          <SectionCard title="应用调用分布" flush>
            <ChartContainer :option="appDistChartOption" height="300px" :loading="loading" />
          </SectionCard>
        </a-col>
      </a-row>

      <a-row :gutter="[16, 16]" class="charts-row">
        <a-col :span="24">
          <SectionCard title="活跃用户趋势" flush>
            <ChartContainer :option="activeUsersChartOption" height="300px" :loading="loading" />
          </SectionCard>
        </a-col>
      </a-row>

      <SectionCard title="最近操作日志">
        <DataTable
          storage-key="admin-dashboard-logs"
          :columns="logColumns"
          :data-source="recentLogs"
          row-key="id"
          :loading="loading"
          :pagination="false"
          :card="false"
        />
      </SectionCard>
    </template>
  </div>
</template>

<script setup lang="ts">
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn } from '@shared/web'
import { ref, computed } from 'vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import MetricCard from '@shared/web/components/MetricCard.vue'
import ChartContainer from '@shared/web/components/ChartContainer.vue'
import { useCssVar } from '@shared/web/composables/useCssVar'
import { useChartTheme } from '@shared/web/composables/useChartTheme'
import { useStaggerReveal } from '@shared/web/composables/useStaggerReveal'
import { useRequest } from '@shared/web/composables/useRequest'
import type { DashboardMetric, SystemLog } from '@/types'
import { getDashboardMetrics, getApiCallsTrend, getAppDistribution, getActiveUsersTrend, getRecentLogs } from '@/api/modules/dashboard'

const { chartTheme } = useChartTheme()

const metrics = ref<DashboardMetric[]>([])
const recentLogs = ref<SystemLog[]>([])

const { itemStyle } = useStaggerReveal(() => metrics.value.length)

const brandColor = useCssVar('--color-brand')
const accentColor = useCssVar('--color-accent')
const successColor = useCssVar('--color-success')
const warningColor = useCssVar('--color-warning')
const dangerColor = useCssVar('--color-danger')

const colors = computed(() => [brandColor.value, accentColor.value, successColor.value, warningColor.value, dangerColor.value])

const apiCallsTrend = ref<{ categories: string[], series: { name: string, data: number[] }[] }>({ categories: [], series: [] })
const appDistribution = ref<{ name: string, data: { name: string, value: number }[] }>({ name: '', data: [] })
const activeUsersTrend = ref<{ categories: string[], series: { name: string, data: number[] }[] }>({ categories: [], series: [] })

const logColumns: DataTableColumn[] = [
  { title: '类型', dataIndex: 'type', key: 'type', width: 110, minWidth: 90, resizable: true },
  { title: '操作人', dataIndex: 'operator', key: 'operator', width: 110, minWidth: 90, resizable: true },
  { title: '内容', dataIndex: 'content', key: 'content', width: 360, minWidth: 240, resizable: true, ellipsis: true },
  { title: '时间', dataIndex: 'createdAt', key: 'createdAt', width: 160, minWidth: 140, resizable: true, responsive: ['md'] },
]

const apiCallsChartOption = computed(() => {
  const t = chartTheme()
  return {
    tooltip: { trigger: 'axis' as const, backgroundColor: t.tooltipBg, borderColor: t.tooltipBorder, borderWidth: 1, textStyle: { color: t.tooltipColor, fontSize: 13 } },
    grid: { left: 40, right: 24, bottom: 24, top: 16 },
    xAxis: { type: 'category' as const, boundaryGap: false, data: apiCallsTrend.value.categories, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11 } },
    yAxis: { type: 'value' as const, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11 }, splitLine: { lineStyle: { color: t.splitColor, type: 'dashed' as const } } },
    series: apiCallsTrend.value.series.map((s) => ({
      name: s.name,
      type: 'line' as const,
      smooth: true,
      data: s.data,
      lineStyle: { color: brandColor.value, width: 2 },
      itemStyle: { color: brandColor.value },
      areaStyle: { color: `${brandColor.value}18` },
      animationDuration: 600,
      animationEasing: 'easeOutQuad' as const,
    })),
  }
})

const appDistChartOption = computed(() => {
  const t = chartTheme()
  const isDark = document.documentElement.getAttribute('data-theme') === 'dark'
  return {
    tooltip: { trigger: 'item' as const, formatter: '{b}: {c} ({d}%)', backgroundColor: t.tooltipBg, borderColor: t.tooltipBorder, borderWidth: 1, textStyle: { color: t.tooltipColor, fontSize: 13 } },
    legend: { bottom: 0, type: 'scroll' as const, textStyle: { color: t.legendColor, fontSize: 12 } },
    series: [{
      type: 'pie' as const,
      radius: ['40%', '70%'],
      center: ['50%', '45%'],
      data: appDistribution.value.data,
      itemStyle: { borderRadius: 6, borderColor: 'transparent', borderWidth: 2 },
      label: { show: false },
      emphasis: { itemStyle: { shadowBlur: 10, shadowColor: isDark ? 'rgba(0,0,0,0.4)' : 'rgba(0,0,0,0.2)' }, label: { show: true, fontSize: 14, fontWeight: 'bold' as const } },
      color: colors.value,
      animationDuration: 600,
    }],
  }
})

const activeUsersChartOption = computed(() => {
  const t = chartTheme()
  const seriesColors = [brandColor.value, accentColor.value, successColor.value]
  return {
    tooltip: { trigger: 'axis' as const, backgroundColor: t.tooltipBg, borderColor: t.tooltipBorder, borderWidth: 1, textStyle: { color: t.tooltipColor, fontSize: 13 } },
    legend: { data: activeUsersTrend.value.series.map((s) => s.name), bottom: 0, textStyle: { color: t.legendColor, fontSize: 12 } },
    grid: { left: 40, right: 24, bottom: 44, top: 16 },
    xAxis: { type: 'category' as const, boundaryGap: false, data: activeUsersTrend.value.categories, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11 } },
    yAxis: { type: 'value' as const, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11 }, splitLine: { lineStyle: { color: t.splitColor, type: 'dashed' as const } } },
    series: activeUsersTrend.value.series.map((s, i) => ({
      name: s.name,
      type: 'line' as const,
      smooth: true,
      data: s.data,
      lineStyle: { color: seriesColors[i % seriesColors.length], width: 2 },
      itemStyle: { color: seriesColors[i % seriesColors.length] },
      areaStyle: { color: `${seriesColors[i % seriesColors.length]}18` },
      animationDuration: 400 + i * 80,
      animationEasing: 'easeOutQuad' as const,
    })),
  }
})

const { loading, error, refresh } = useRequest('admin-dashboard', async () => {
  const [m, t, d, a, logs] = await Promise.all([
    getDashboardMetrics(),
    getApiCallsTrend(),
    getAppDistribution(),
    getActiveUsersTrend(),
    getRecentLogs(),
  ])
  metrics.value = m
  apiCallsTrend.value = t
  appDistribution.value = d
  activeUsersTrend.value = a
  recentLogs.value = logs
  return m
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.metrics-row { margin-bottom: @spacing-lg; }
.charts-row { margin-bottom: @spacing-lg; }
</style>
