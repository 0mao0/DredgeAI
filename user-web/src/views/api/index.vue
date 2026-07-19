<template>
  <div class="page-container">
    <PageHeader title="API 管理" description="管理 API Key、查看模型调用统计">
      <template #extra>
        <a-button type="primary" @click="showCreateModal = true">
          <plus-outlined />
          创建 Key
        </a-button>
      </template>
    </PageHeader>

    <a-spin :spinning="loading" tip="加载中...">
      <SectionCard title="API Key 列表" nopad class="mb-24">
        <a-table
          :data-source="apiKeys"
          :columns="columns"
          :pagination="{ pageSize: 10 }"
          row-key="id"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'key'">
              <code class="key-text">{{ record.key }}</code>
              <a-button type="link" size="small" @click="copyKey(record.fullKey)">
                <copy-outlined />
              </a-button>
            </template>
            <template v-else-if="column.key === 'status'">
              <a-tag :color="record.status === '启用' ? 'green' : 'red'">{{ record.status }}</a-tag>
            </template>
            <template v-else-if="column.key === 'doc'">
              <a-button type="link" size="small" :href="record.docUrl" target="_blank">
                <file-text-outlined /> 文档
              </a-button>
            </template>
            <template v-else-if="column.key === 'action'">
              <a-button type="link" size="small">编辑</a-button>
              <a-button type="link" size="small" :danger="record.status === '启用'">
                {{ record.status === '启用' ? '禁用' : '启用' }}
              </a-button>
            </template>
          </template>
        </a-table>
      </SectionCard>

      <div class="stats-section">
        <div class="stats-header">
          <h3 class="stats-title">统计分析</h3>
          <div class="time-range-wrap">
            <a-radio-group v-model:value="timeRange" size="small" @change="onTimeRangeChange">
              <a-radio-button value="7d">近7日</a-radio-button>
              <a-radio-button value="30d">近30日</a-radio-button>
              <a-radio-button value="this-month">本月</a-radio-button>
              <a-radio-button value="last-month">上月</a-radio-button>
              <a-radio-button value="custom">自定义</a-radio-button>
            </a-radio-group>
            <a-range-picker
              v-if="timeRange === 'custom'"
              v-model:value="customDateRange"
              size="small"
              class="custom-date-picker"
              :allow-empty="false"
            />
          </div>
        </div>

        <a-row :gutter="24" class="mb-24">
          <a-col :span="12">
            <div class="stat-card">
              <div class="stat-label">总 Token 用量</div>
              <div class="stat-value">{{ formatNum(usageStats?.totalTokens || 0) }}</div>
            </div>
          </a-col>
          <a-col :span="12">
            <div class="stat-card">
              <div class="stat-label">总 API 调用次数</div>
              <div class="stat-value">{{ formatNum(usageStats?.totalCalls || 0) }}</div>
            </div>
          </a-col>
        </a-row>

        <a-row :gutter="24">
          <a-col :span="12" class="mb-24">
            <SectionCard title="按模型调用趋势" nopad>
              <ChartContainer :option="byModelOption" height="280px" />
            </SectionCard>
          </a-col>
          <a-col :span="12" class="mb-24">
            <SectionCard title="按 API Key 调用趋势" nopad>
              <ChartContainer :option="byKeyOption" height="280px" />
            </SectionCard>
          </a-col>
          <a-col :span="12" class="mb-24">
            <SectionCard title="按名称调用趋势" nopad>
              <ChartContainer :option="byNameOption" height="280px" />
            </SectionCard>
          </a-col>
          <a-col :span="12" class="mb-24">
            <SectionCard title="模型总调用占比" nopad>
              <ChartContainer :option="pieOption" height="280px" />
            </SectionCard>
          </a-col>
        </a-row>
      </div>
    </a-spin>

    <a-modal v-model:open="showCreateModal" title="创建 API Key" @ok="handleCreate">
      <a-form layout="vertical">
        <a-form-item label="Key 名称" required>
          <a-input v-model:value="newKey.name" placeholder="如：生产环境-主入口" />
        </a-form-item>
        <a-form-item label="模型类型" required>
          <a-select v-model:value="newKey.modelType" :options="modelOptions" placeholder="选择模型" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted } from 'vue'
import dayjs from 'dayjs'
import { message } from 'ant-design-vue'
import { PlusOutlined, CopyOutlined, FileTextOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
import ChartContainer from '@shared/components/ChartContainer.vue'
import { getApiKeyList, getModelTypes, getUsageByModel, getUsageStats, getUsageTimeSeries } from '@/api/modules/apikey'
import type { ApiKey, ModelType, UsageByModel, ApiUsageStats, UsageTimeSeries } from '@/types'
import { useCssVar } from '@shared/composables/useCssVar'

const apiKeys = ref<ApiKey[]>([])
const modelTypes = ref<ModelType[]>([])
const usageByModel = ref<UsageByModel[]>([])
const usageStats = ref<ApiUsageStats | null>(null)
const usageTimeSeriesData = ref<UsageTimeSeries | null>(null)
const showCreateModal = ref(false)
const timeRange = ref('7d')
const loading = ref(true)

const newKey = ref({ name: '', modelType: '' })
const customDateRange = ref<[dayjs.Dayjs, dayjs.Dayjs]>()

function onTimeRangeChange(): void {
  if (timeRange.value !== 'custom') {
    loadTimeSeries()
  }
}

watch(customDateRange, (val) => {
  if (val?.[0] && val?.[1] && timeRange.value === 'custom') {
    loadTimeSeries()
  }
})

const columns = [
  { title: '名称', dataIndex: 'name', key: 'name' },
  { title: 'Key', key: 'key', width: 180 },
  { title: '模型', dataIndex: 'modelType', key: 'modelType' },
  { title: '状态', key: 'status', width: 80 },
  { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt' },
  { title: 'API 文档', key: 'doc', width: 100 },
  { title: '操作', key: 'action', width: 120 },
]

const modelOptions = computed(() => modelTypes.value.map((m) => ({ label: m.name, value: m.name })))

function formatNum(n: number): string {
  if (n >= 10000) return (n / 10000).toFixed(1) + '万'
  if (n >= 1000) return (n / 1000).toFixed(1) + 'k'
  return n.toLocaleString()
}

const brandColor = useCssVar('--color-brand')
const successColor = useCssVar('--color-success')
const accentColor = useCssVar('--color-accent')
const warningColor = useCssVar('--color-warning')
const dangerColor = useCssVar('--color-danger')
const cardBgColor = useCssVar('--color-card-bg')

const colors = computed(() => [brandColor.value, accentColor.value, successColor.value, warningColor.value, dangerColor.value])

function makeTimeSeriesOption(data: { name: string; data: number[] }[] | undefined, categories: string[] | undefined) {
  if (!data || !categories) return {}
  return {
    tooltip: { trigger: 'axis' },
    legend: { type: 'scroll', bottom: 0 },
    grid: { left: '3%', right: '4%', bottom: '18%', top: '5%', containLabel: true },
    xAxis: { type: 'category', data: categories, axisLabel: { fontSize: 11 } },
    yAxis: { type: 'value' },
    series: data.map((s, i) => ({
      name: s.name,
      type: 'bar',
      data: s.data,
      itemStyle: { color: colors.value[i % colors.value.length], borderRadius: [3, 3, 0, 0] },
      barWidth: '30%',
    })),
  }
}

const byModelOption = computed(() =>
  makeTimeSeriesOption(
    usageTimeSeriesData.value?.byModel?.map((s) => ({ name: s.modelName, data: s.data })),
    usageTimeSeriesData.value?.categories))

const byKeyOption = computed(() =>
  makeTimeSeriesOption(
    usageTimeSeriesData.value?.byKey?.map((s) => ({ name: s.keyName, data: s.data })),
    usageTimeSeriesData.value?.categories))

const byNameOption = computed(() =>
  makeTimeSeriesOption(usageTimeSeriesData.value?.byName, usageTimeSeriesData.value?.categories))

const pieOption = computed(() => ({
  tooltip: { trigger: 'item', formatter: '{b}: {c} ({d}%)' },
  legend: { bottom: 0, type: 'scroll' },
  series: [{
    type: 'pie',
    radius: ['40%', '70%'],
    center: ['50%', '45%'],
    avoidLabelOverlap: false,
    itemStyle: { borderRadius: 6, borderColor: cardBgColor.value, borderWidth: 2 },
    label: { show: false },
    emphasis: { label: { show: true, fontSize: 14, fontWeight: 'bold' } },
    data: usageByModel.value.map((u) => ({ name: u.modelName, value: u.calls })),
    color: colors.value,
  }],
}))

function copyKey(key: string): void {
  navigator.clipboard.writeText(key)
  message.success('已复制到剪贴板')
}

function handleCreate(): void {
  if (!newKey.value.name || !newKey.value.modelType) {
    message.warning('请填写完整信息')
    return
  }
  message.success('API Key 创建成功')
  showCreateModal.value = false
  newKey.value = { name: '', modelType: '' }
}

async function loadTimeSeries(): Promise<void> {
  if (timeRange.value === 'custom' && customDateRange.value?.[0] && customDateRange.value?.[1]) {
    const fmt = 'YYYY-MM-DD'
    usageTimeSeriesData.value = await getUsageTimeSeries('custom', {
      startDate: customDateRange.value[0].format(fmt),
      endDate: customDateRange.value[1].format(fmt),
    })
  } else {
    usageTimeSeriesData.value = await getUsageTimeSeries(timeRange.value)
  }
}

onMounted(async () => {
  try {
    loading.value = true
    const [k, m, um, stats] = await Promise.all([
      getApiKeyList(), getModelTypes(), getUsageByModel(), getUsageStats(),
    ])
    apiKeys.value = k
    modelTypes.value = m
    usageByModel.value = um
    usageStats.value = stats
    await loadTimeSeries()
  } catch (e) {
    console.error('[API] 数据加载失败', e)
  } finally {
    loading.value = false
  }
})
</script>

<style scoped lang="less">
@import '@shared/styles/variables.less';

.mb-24 { margin-bottom: @spacing-xl; }

.key-text {
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: @font-size-xs;
  color: @text-primary;
  background: @content-bg;
  padding: 2px @spacing-sm;
  border-radius: @radius-sm;
}

.stats-section {
  background: @card-bg;
  border-radius: @radius-lg;
  border: 1px solid @border-color;
  box-shadow: @shadow-sm;
  padding: @spacing-xl;
}

.stats-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: @spacing-xl;
}

.time-range-wrap {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
}
.custom-date-picker {
  min-width: 200px;
}

.stats-title {
  font-size: @font-size-lg;
  font-weight: @font-weight-semibold;
  color: @text-primary;
  margin: 0;
}

.stat-card {
  background: @content-bg;
  border-radius: @radius-base;
  padding: @spacing-lg @spacing-xl;
  display: flex;
  flex-direction: column;
  gap: @spacing-xs;
}

.stat-label {
  font-size: @font-size-sm;
  color: @text-secondary;
}

.stat-value {
  font-size: @font-size-3xl;
  font-weight: @font-weight-bold;
  color: @text-primary;
  line-height: 1.2;
}
</style>
