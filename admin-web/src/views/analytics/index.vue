<template>
  <div class="page-container">
    <PageHeader title="数据分析" description="系统数据统计与分析" />

    <a-row :gutter="[16, 16]">
      <a-col :span="14">
        <SectionCard title="API 调用曲线（今日 vs 昨日）">
          <ChartContainer :option="dailyApiOption" height="300px" :loading="loading" />
        </SectionCard>
      </a-col>
      <a-col :span="10">
        <SectionCard title="模型调用分布">
          <ChartContainer :option="modelUsageOption" height="300px" :loading="loading" />
        </SectionCard>
      </a-col>
    </a-row>

    <a-row :gutter="[16, 16]" class="chart-row">
      <a-col :span="12">
        <SectionCard title="用户增长趋势">
          <ChartContainer :option="userGrowthOption" height="280px" :loading="loading" />
        </SectionCard>
      </a-col>
      <a-col :span="12">
        <SectionCard title="错误率监控">
          <ChartContainer :option="errorRateOption" height="280px" :loading="loading" />
        </SectionCard>
      </a-col>
    </a-row>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import ChartContainer from '@shared/web/components/ChartContainer.vue'
import { useCssVar } from '@shared/web/composables/useCssVar'
import { getDailyApiCalls, getModelUsage, getUserGrowth, getErrorRate } from '@/api/modules/analytics'

const loading = ref(false)
const dailyApi = ref<{ categories: string[], series: { name: string, data: number[] }[] }>({ categories: [], series: [] })

const brandColor = useCssVar('--color-brand')
const dangerColor = useCssVar('--color-danger')
const modelUsage = ref<{ name: string, data: { name: string, value: number }[] }>({ name: '', data: [] })
const userGrowth = ref<{ categories: string[], series: { name: string, data: number[] }[] }>({ categories: [], series: [] })
const errorRate = ref<{ categories: string[], series: { name: string, data: number[] }[] }>({ categories: [], series: [] })

const dailyApiOption = computed(() => ({
  tooltip: { trigger: 'axis' as const },
  legend: { data: dailyApi.value.series.map((s) => s.name), bottom: 0 },
  grid: { left: '3%', right: '4%', bottom: '15%', top: '5%', containLabel: true },
  xAxis: { type: 'category' as const, boundaryGap: false, data: dailyApi.value.categories },
  yAxis: { type: 'value' as const },
  series: dailyApi.value.series.map((s, i) => ({
    name: s.name,
    type: 'line' as const,
    smooth: true,
    data: s.data,
    itemStyle: { color: i === 0 ? brandColor.value : '#94A3B8' },
    areaStyle: { opacity: 0.08 },
  })),
}))

const modelUsageOption = computed(() => ({
  tooltip: { trigger: 'item' as const, formatter: '{b}: {c}%' },
  series: [{
    type: 'pie' as const,
    radius: ['40%', '70%'],
    data: modelUsage.value.data,
    itemStyle: { borderRadius: 4 },
    label: { show: true, formatter: '{b}\n{d}%' },
  }],
}))

const userGrowthOption = computed(() => ({
  tooltip: { trigger: 'axis' as const },
  grid: { left: '3%', right: '4%', bottom: '3%', top: '5%', containLabel: true },
  xAxis: { type: 'category' as const, data: userGrowth.value.categories },
  yAxis: { type: 'value' as const },
  series: userGrowth.value.series.map((s) => ({
    name: s.name,
    type: 'bar' as const,
    data: s.data,
    itemStyle: { color: brandColor.value, borderRadius: [4, 4, 0, 0] },
  })),
}))

const errorRateOption = computed(() => ({
  tooltip: { trigger: 'axis' as const, valueFormatter: (v: number) => `${v}%` },
  grid: { left: '3%', right: '4%', bottom: '3%', top: '5%', containLabel: true },
  xAxis: { type: 'category' as const, data: errorRate.value.categories },
  yAxis: { type: 'value' as const, axisLabel: { formatter: '{value}%' } },
  series: errorRate.value.series.map((s) => ({
    name: s.name,
    type: 'line' as const,
    smooth: true,
    data: s.data.map((v) => +(v * 100).toFixed(1)),
    itemStyle: { color: dangerColor.value },
    areaStyle: { opacity: 0.1 },
  })),
}))

onMounted(async () => {
  loading.value = true
  const [d, m, u, e] = await Promise.all([
    getDailyApiCalls(),
    getModelUsage(),
    getUserGrowth(),
    getErrorRate(),
  ])
  dailyApi.value = d
  modelUsage.value = m
  userGrowth.value = u
  errorRate.value = e
  loading.value = false
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';
.chart-row {
  margin-top: @spacing-lg;
}
</style>
