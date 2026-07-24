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

    <SectionCard nopad class="mb-24">
      <a-table
        :data-source="apiKeys"
        :columns="columns"
        :pagination="{ pageSize: 10 }"
        row-key="id"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'key'">
            <code class="key-text">{{ record.key }}</code>
          </template>
          <template v-else-if="column.key === 'doc'">
            <a-button type="link" size="small" @click="openDoc(record.docUrl)">
              <file-text-outlined /> 文档
            </a-button>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-button type="link" size="small" @click="handleEdit(record)">编辑</a-button>
            <a-popconfirm title="确认删除？" @confirm="handleDelete(record.id)">
              <a-button type="link" size="small" danger>删除</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </SectionCard>

    <a-row :gutter="16" class="mb-24">
      <a-col :span="12">
          <MetricCard
            title="总调用次数"
            :value="formatNumber(totalCalls)"
            suffix="次"
            icon="ThunderboltOutlined"
            :color="brandColor"
          />
      </a-col>
      <a-col :span="12">
          <MetricCard
            title="总 Token 消耗量"
            :value="formatNumber(totalTokens)"
            suffix="tokens"
            icon="DatabaseOutlined"
            :color="accentColor"
          />
      </a-col>
    </a-row>

    <SectionCard title="调用趋势">
      <div class="chart-header">
        <a-radio-group v-model:value="chartMode" size="small">
          <a-radio-button value="model">按模型</a-radio-button>
          <a-radio-button value="key">按 API Key</a-radio-button>
          <a-radio-button value="total">调用次数</a-radio-button>
        </a-radio-group>
        <div class="time-range-wrap">
          <a-radio-group v-model:value="timeRange" size="small">
            <a-radio-button value="7d">近7日</a-radio-button>
            <a-radio-button value="30d">近30日</a-radio-button>
            <a-radio-button value="month">本月</a-radio-button>
            <a-radio-button value="prevMonth">上月</a-radio-button>
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
      <ChartContainer :option="chartOption" height="320px" />
    </SectionCard>

    <a-modal v-model:open="showCreateModal" title="创建 API Key" @ok="handleCreate" @cancel="newKey = { name: '', modelType: '' }">
      <a-form layout="vertical">
        <a-form-item label="Key 名称" required>
          <a-input v-model:value="newKey.name" placeholder="如：生产环境-主入口" />
        </a-form-item>
        <a-form-item label="模型类型" required>
          <a-select v-model:value="newKey.modelType" :options="modelOptions" placeholder="选择模型" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal v-model:open="showCopyModal" title="创建成功" :footer="null" width="520px" @cancel="newKey = { name: '', modelType: '' }">
      <div class="copy-modal-body">
        <p class="copy-warning">
          请将此 API key 保存在<span class="highlight">安全且易于访问</span>的地方。出于安全原因，你将<strong>无法</strong>通过 API keys 管理界面再次查看它。如果你丟失了这个 key，将需要<strong>重新创建</strong>。
        </p>
        <div class="key-box">
          <code>{{ createdKey }}</code>
        </div>
        <a-button type="primary" block @click="copyCreatedKey">
          <copy-outlined /> 复制 Key
        </a-button>
      </div>
    </a-modal>

    <a-modal v-model:open="showEditModal" title="编辑 API Key" @ok="handleEditOk" @cancel="editTarget = null">
      <a-form layout="vertical">
        <a-form-item label="Key 名称" required>
          <a-input v-model:value="editName" placeholder="输入新名称" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, CopyOutlined, FileTextOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import ChartContainer from '@shared/web/components/ChartContainer.vue'
import MetricCard from '@shared/web/components/MetricCard.vue'
import { useCssVar } from '@shared/web/composables/useCssVar'
import { useChartTheme } from '@shared/web/composables/useChartTheme'
import { formatNumber } from '@shared/core/utils/format'
import { getApiKeyList, getUsageStats, getUsageTimeSeries } from '@/api/modules/apikey'
import type { ApiKey, UsageTimeSeries } from '@/types'

const columns = [
  { title: '名称', dataIndex: 'name', key: 'name', align: 'center' },
  { title: 'Key', key: 'key', width: 240, align: 'center' },
  { title: '模型', dataIndex: 'modelType', key: 'modelType', align: 'center' },
  { title: '创建日期', dataIndex: 'createdAt', key: 'createdAt', width: 120, align: 'center' },
  { title: 'API 文档', key: 'doc', width: 110, align: 'center' },
  { title: '操作', key: 'action', width: 130, align: 'center' },
]

const apiKeys = ref<ApiKey[]>([])
const loading = ref(true)

const modelOptions = [
  { label: 'GPT-4o', value: 'GPT-4o' },
  { label: 'GPT-4o-mini', value: 'GPT-4o-mini' },
  { label: 'Claude-3.5-Sonnet', value: 'Claude-3.5-Sonnet' },
  { label: 'DeepSeek-V3', value: 'DeepSeek-V3' },
]

const showCreateModal = ref(false)
const showCopyModal = ref(false)
const showEditModal = ref(false)
const createdKey = ref('')
const newKey = ref({ name: '', modelType: '' })
const editTarget = ref<{ id: string; name: string } | null>(null)
const editName = ref('')
const chartMode = ref<'model' | 'key' | 'total'>('model')
const timeRange = ref('7d')
const customDateRange = ref()

const { chartTheme } = useChartTheme()

const brandColor = useCssVar('--color-brand')
const successColor = useCssVar('--color-success')
const accentColor = useCssVar('--color-accent')
const warningColor = useCssVar('--color-warning')
const dangerColor = useCssVar('--color-danger')

const colors = computed(() => [brandColor.value, accentColor.value, successColor.value, warningColor.value, dangerColor.value])

// ─── Data Fetching ──────────────────────────────────────

const usageStats = ref({ totalCalls: 0, totalTokens: 0 })
const timeSeriesData = ref<UsageTimeSeries | null>(null)

onMounted(async () => {
  try {
    const [keys, stats] = await Promise.all([
      getApiKeyList(),
      getUsageStats(),
    ])
    apiKeys.value = keys
    usageStats.value = stats
  } catch {
    message.error('加载 API 数据失败')
  } finally {
    loading.value = false
  }
  await fetchTimeSeries()
})

async function fetchTimeSeries() {
  try {
    const params: Record<string, string> = { range: timeRange.value }
    if (timeRange.value === 'custom' && customDateRange.value) {
      params.startDate = customDateRange.value[0]?.format('YYYY-MM-DD')
      params.endDate = customDateRange.value[1]?.format('YYYY-MM-DD')
    }
    timeSeriesData.value = await getUsageTimeSeries(timeRange.value, params)
  } catch {
    message.error('加载趋势数据失败')
  }
}

watch(timeRange, () => fetchTimeSeries())

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

const totalCalls = computed(() => {
  const data = timeSeriesData.value
  if (!data) return 0
  if (chartMode.value === 'total') {
    // 总调用次数 = 所有模型数据之和
    return data.byModel.reduce((sum: number, m) => sum + m.data.reduce((a: number, b: number) => a + b, 0), 0)
  }
  const series = chartMode.value === 'model' ? data.byModel : data.byName
  return series.reduce((sum: number, s: { data: number[] }) => sum + s.data.reduce((a: number, b: number) => a + b, 0), 0)
})

const totalTokens = computed(() => usageStats.value.totalTokens)

const chartOption = computed(() => {
  const data = timeSeriesData.value
  if (!data) return {}
  const categories = data.categories
  const t = chartTheme()

  const base = () => ({
    tooltip: { trigger: 'axis', backgroundColor: t.tooltipBg, borderColor: t.tooltipBorder, borderWidth: 1, textStyle: { color: t.tooltipColor, fontSize: 13 }, valueFormatter: (v: number) => `${v.toLocaleString()} 次` },
    grid: { left: 40, right: 16, bottom: 24, top: 12 },
    xAxis: { type: 'category', data: categories, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11 } },
    yAxis: { type: 'value', min: 0, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11 }, splitLine: { lineStyle: { color: t.splitColor, type: 'dashed' } } },
  })

  if (chartMode.value === 'total') {
    // 总调用次数 = 所有 byModel 数据逐日求和
    const totalData = data.byModel[0]?.data.map((_: number, i: number) =>
      data.byModel.reduce((sum: number, m: { data: number[] }) => sum + (m.data[i] || 0), 0)
    ) || []
    return {
      ...base(), legend: undefined,
      series: [{ type: 'bar', data: totalData, barWidth: '32%', itemStyle: { color: makeBarGradient(brandColor.value), borderRadius: [6, 6, 0, 0] }, animationDuration: 600, animationEasing: 'easeOutQuad' }],
    }
  }

  const series = chartMode.value === 'model' ? data.byModel.map(m => ({ name: m.modelName, data: m.data })) : data.byName
  const count = series.length
  return {
    ...base(),
    grid: { left: 40, right: 16, bottom: 44, top: 12 },
    legend: { type: 'scroll', bottom: 0, textStyle: { color: t.legendColor, fontSize: 12 } },
    series: series.map((s: { name: string; data: number[] }, i: number) => ({
      name: s.name, type: 'bar', stack: 'total', data: s.data, barWidth: '44%',
      itemStyle: { color: makeBarGradient(colors.value[i % colors.value.length]), borderRadius: i === count - 1 ? [6, 6, 0, 0] : 0 },
      animationDuration: 400 + i * 80, animationEasing: 'easeOutQuad',
    })),
  }
})

function openDoc(url: string): void {
  window.open(url, '_blank')
}

function handleDelete(id: string): void {
  apiKeys.value = apiKeys.value.filter((k) => k.id !== id)
  message.success('已删除')
}

function handleEdit(record: { id: string; name: string }): void {
  editTarget.value = record
  editName.value = record.name
  showEditModal.value = true
}

function handleEditOk(): void {
  if (!editName.value.trim()) {
    message.warning('名称不能为空')
    return
  }
  if (editTarget.value) {
    const item = apiKeys.value.find((k) => k.id === editTarget.value!.id)
    if (item) item.name = editName.value.trim()
  }
  message.success('已更新')
  showEditModal.value = false
  editTarget.value = null
}

function handleCreate(): void {
  if (!newKey.value.name || !newKey.value.modelType) {
    message.warning('请填写完整信息')
    return
  }
  createdKey.value = 'sk-dg-' + Array.from({ length: 20 }, () => 'abcdefghijklmnopqrstuvwxyz0123456789'[Math.floor(Math.random() * 36)]).join('')
  showCreateModal.value = false
  showCopyModal.value = true
}

function copyCreatedKey(): void {
  navigator.clipboard.writeText(createdKey.value)
  message.success('已复制到剪贴板')
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.mb-24 { margin-bottom: @spacing-xl; }

.key-text {
  font-family: 'Consolas', 'Monaco', monospace;
  font-size: @font-size-xs;
  color: @text-primary;
  background: @content-bg;
  padding: 2px @spacing-sm;
  border-radius: @radius-sm;
}

.chart-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: @spacing-lg;
}

.time-range-wrap {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
}
.custom-date-picker {
  min-width: 200px;
}

.copy-modal-body {
  p { margin-bottom: 16px; }
  .highlight { color: var(--color-warning, #f59e0b); font-weight: 600; }
  strong { color: var(--color-danger, #ef4444); }
  .key-box {
    background: var(--content-bg);
    border: 1px solid var(--border-color);
    border-radius: var(--radius-sm);
    padding: 12px 16px;
    margin-bottom: 16px;
    code {
      font-family: 'Consolas', 'Monaco', monospace;
      font-size: 13px;
      word-break: break-all;
      color: var(--text-primary);
    }
  }
}
</style>
