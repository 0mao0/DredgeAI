<template>
  <div class="page-container">
    <PageHeader title="仪表盘" description="系统运营概览">
      <template #extra>
        <a-button type="primary" @click="refresh">刷新数据</a-button>
      </template>
    </PageHeader>

    <a-row :gutter="[16, 16]" class="metrics-row">
      <a-col :xs="24" :sm="12" :lg="6" v-for="m in metrics" :key="m.id">
        <MetricCard v-bind="m" />
      </a-col>
    </a-row>

    <a-row :gutter="[16, 16]" class="charts-row">
      <a-col :span="16">
        <SectionCard title="API 调用趋势">
          <ChartContainer :option="apiCallsChartOption" height="320px" :loading="loading" />
        </SectionCard>
      </a-col>
      <a-col :span="8">
        <SectionCard title="应用调用分布">
          <ChartContainer :option="appDistChartOption" height="320px" :loading="loading" />
        </SectionCard>
      </a-col>
    </a-row>

    <a-row :gutter="[16, 16]" class="charts-row">
      <a-col :span="24">
        <SectionCard title="活跃用户趋势">
          <ChartContainer :option="activeUsersChartOption" height="300px" :loading="loading" />
        </SectionCard>
      </a-col>
    </a-row>

    <SectionCard title="最近操作日志">
      <a-table
        :data-source="recentLogs"
        :columns="logColumns"
        :pagination="false"
        size="small"
        :loading="loading"
      />
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
import MetricCard from '@shared/components/MetricCard.vue'
import ChartContainer from '@shared/components/ChartContainer.vue'
import type { DashboardMetric, SystemLog } from '@/types'
import { getDashboardMetrics, getApiCallsTrend, getAppDistribution, getActiveUsersTrend, getRecentLogs } from '@/api/modules/dashboard'

const loading = ref(false)
const metrics = ref<DashboardMetric[]>([])
const recentLogs = ref<SystemLog[]>([])

const apiCallsTrend = ref<{ categories: string[]; series: { name: string; data: number[] }[] }>({ categories: [], series: [] })
const appDistribution = ref<{ name: string; data: { name: string; value: number }[] }>({ name: '', data: [] })
const activeUsersTrend = ref<{ categories: string[]; series: { name: string; data: number[] }[] }>({ categories: [], series: [] })

const logColumns = [
  { title: '类型', dataIndex: 'type', key: 'type', width: 100 },
  { title: '操作人', dataIndex: 'operator', key: 'operator', width: 100 },
  { title: '内容', dataIndex: 'content', key: 'content', ellipsis: true },
  { title: '时间', dataIndex: 'createdAt', key: 'createdAt', width: 160 },
]

const apiCallsChartOption = computed(() => ({
  tooltip: { trigger: 'axis' as const },
  grid: { left: '3%', right: '4%', bottom: '3%', top: '5%', containLabel: true },
  xAxis: { type: 'category' as const, boundaryGap: false, data: apiCallsTrend.value.categories },
  yAxis: { type: 'value' as const },
  series: apiCallsTrend.value.series.map((s) => ({
    name: s.name,
    type: 'line' as const,
    smooth: true,
    data: s.data,
    itemStyle: { color: '#0EA5E9' },
    areaStyle: { color: 'rgba(14, 165, 233, 0.1)' },
  })),
}))

const appDistChartOption = computed(() => ({
  tooltip: { trigger: 'item' as const, formatter: '{b}: {c}% ({d}%)' },
  series: [{
    type: 'pie' as const,
    radius: ['40%', '70%'],
    center: ['50%', '50%'],
    data: appDistribution.value.data,
    itemStyle: { borderRadius: 4, borderColor: 'transparent', borderWidth: 2 },
    label: { show: true, formatter: '{b}\n{d}%' },
    emphasis: { itemStyle: { shadowBlur: 10, shadowColor: 'rgba(0,0,0,0.2)' } },
  }],
}))

const activeUsersChartOption = computed(() => ({
  tooltip: { trigger: 'axis' as const },
  legend: { data: activeUsersTrend.value.series.map((s) => s.name), bottom: 0 },
  grid: { left: '3%', right: '4%', bottom: '15%', top: '5%', containLabel: true },
  xAxis: { type: 'category' as const, boundaryGap: false, data: activeUsersTrend.value.categories },
  yAxis: { type: 'value' as const },
  series: activeUsersTrend.value.series.map((s, i) => ({
    name: s.name,
    type: 'line' as const,
    smooth: true,
    data: s.data,
    itemStyle: { color: i === 0 ? '#0EA5E9' : '#10B981' },
    areaStyle: { opacity: 0.08 },
  })),
}))

async function fetchData(): Promise<void> {
  loading.value = true
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
  loading.value = false
}

async function refresh(): Promise<void> {
  await fetchData()
}

onMounted(fetchData)
</script>

<style scoped lang="less">
@import '@shared/styles/variables.less';

.metrics-row { margin-bottom: @spacing-lg; }
.charts-row { margin-bottom: @spacing-lg; }
</style>
