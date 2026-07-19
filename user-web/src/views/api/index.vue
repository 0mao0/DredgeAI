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
import { ref, computed } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, CopyOutlined, FileTextOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
import ChartContainer from '@shared/components/ChartContainer.vue'
import MetricCard from '@shared/components/MetricCard.vue'
import { useCssVar } from '@shared/composables/useCssVar'
import { formatNumber } from '@shared/utils/format'

const columns = [
  { title: '名称', dataIndex: 'name', key: 'name', align: 'center' },
  { title: 'Key', key: 'key', width: 240, align: 'center' },
  { title: '模型', dataIndex: 'model', key: 'model', align: 'center' },
  { title: '创建日期', dataIndex: 'createdAt', key: 'createdAt', width: 120, align: 'center' },
  { title: 'API 文档', key: 'doc', width: 110, align: 'center' },
  { title: '操作', key: 'action', width: 130, align: 'center' },
]

const mockKeys = [
  { id: '1', name: '生产环境-主入口', key: 'sk-dg-xxxxxxxxxxxx1', model: 'GPT-4o', createdAt: '2026-06-01', docUrl: 'https://docs.example.com/gpt4o' },
  { id: '2', name: '生产环境-备用', key: 'sk-dg-xxxxxxxxxxxx2', model: 'GPT-4o-mini', createdAt: '2026-06-10', docUrl: 'https://docs.example.com/gpt4o-mini' },
  { id: '3', name: '测试环境', key: 'sk-dg-xxxxxxxxxxxx3', model: 'Claude-3.5-Sonnet', createdAt: '2026-06-15', docUrl: 'https://docs.example.com/claude35' },
  { id: '4', name: '内部工具-AI助手', key: 'sk-dg-xxxxxxxxxxxx4', model: 'DeepSeek-V3', createdAt: '2026-06-20', docUrl: 'https://docs.example.com/deepseek' },
  { id: '5', name: '数据分析管道', key: 'sk-dg-xxxxxxxxxxxx5', model: 'GPT-4o', createdAt: '2026-07-01', docUrl: 'https://docs.example.com/gpt4o' },
]

const apiKeys = ref(mockKeys)

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

const brandColor = useCssVar('--color-brand')
const successColor = useCssVar('--color-success')
const accentColor = useCssVar('--color-accent')
const warningColor = useCssVar('--color-warning')
const dangerColor = useCssVar('--color-danger')

const colors = computed(() => [brandColor.value, accentColor.value, successColor.value, warningColor.value, dangerColor.value])

function generateDays(n: number, offset = 0): string[] {
  const days: string[] = []
  const ref = new Date()
  ref.setDate(ref.getDate() + offset)
  for (let i = n - 1; i >= 0; i--) {
    const d = new Date(ref)
    d.setDate(d.getDate() - i)
    days.push(`${d.getMonth() + 1}/${d.getDate()}`)
  }
  return days
}

function makeMockSeries(base: number, variance: number, len: number): number[] {
  return Array.from({ length: len }, () => Math.max(0, base + Math.round((Math.random() - 0.5) * variance)))
}

function daysInMonth(y: number, m: number): number {
  return new Date(y, m, 0).getDate()
}

const days30 = generateDays(30)
const days7 = generateDays(7)
const daysMonth = computed(() => {
  const now = new Date()
  const n = now.getDate()
  return generateDays(n)
})
const daysPrevMonth = computed(() => {
  const now = new Date()
  const y = now.getFullYear()
  const m = now.getMonth()
  const n = daysInMonth(y, m)
  return generateDays(n, -n)
})

const mockChartData = computed(() => {
  let days: string[]
  switch (timeRange.value) {
    case '7d': days = days7; break
    case 'month': days = daysMonth.value; break
    case 'prevMonth': days = daysPrevMonth.value; break
    default: days = days30
  }
  const len = days.length
  return {
    categories: days,
    byModel: [
      { name: 'GPT-4o', data: makeMockSeries(320, 200, len) },
      { name: 'GPT-4o-mini', data: makeMockSeries(580, 300, len) },
      { name: 'Claude-3.5-Sonnet', data: makeMockSeries(180, 120, len) },
      { name: 'DeepSeek-V3', data: makeMockSeries(260, 160, len) },
    ],
    byKey: [
      { name: 'sk-dg-xxxx1 (生产-主)', data: makeMockSeries(450, 250, len) },
      { name: 'sk-dg-xxxx2 (生产-备)', data: makeMockSeries(200, 150, len) },
      { name: 'sk-dg-xxxx3 (测试)', data: makeMockSeries(380, 200, len) },
      { name: 'sk-dg-xxxx4 (内部)', data: makeMockSeries(160, 100, len) },
      { name: 'sk-dg-xxxx5 (数据)', data: makeMockSeries(140, 80, len) },
    ],
    total: makeMockSeries(1300, 400, len),
  }
})

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
  const data = mockChartData.value
  if (chartMode.value === 'total') {
    return data.total.reduce((s, v) => s + v, 0)
  }
  const series = chartMode.value === 'model' ? data.byModel : data.byKey
  return series.reduce((sum, s) => sum + s.data.reduce((a, b) => a + b, 0), 0)
})

// 每调用 1 次的 Token 消耗（按模型/Key 估算），使 Token 随维度筛选联动
const tokensPerCall = computed(() => {
  const data = mockChartData.value
  if (chartMode.value === 'total') return 850
  const series = chartMode.value === 'model' ? data.byModel : data.byKey
  const weights = series.map((s) =>
    s.name.includes('mini') || s.name.includes('DeepSeek') || s.name.includes('测试')
      ? 320
      : s.name.includes('GPT-4o') || s.name.includes('生产')
        ? 1100
        : 760,
  )
  const totals = series.map((s) => s.data.reduce((a, b) => a + b, 0))
  const sumCalls = totals.reduce((a, b) => a + b, 0)
  if (sumCalls === 0) return 850
  const sumTokens = totals.reduce((a, b, i) => a + b * weights[i], 0)
  return sumTokens / sumCalls
})

const totalTokens = computed(() => Math.round(totalCalls.value * tokensPerCall.value))

const chartOption = computed(() => {
  const data = mockChartData.value
  const categories = data.categories
  const isDark = document.documentElement.getAttribute('data-theme') === 'dark'
  const axisColor = isDark ? '#52627A' : '#A8A29E'
  const splitColor = isDark ? 'rgba(148, 163, 184, 0.08)' : 'rgba(0, 0, 0, 0.06)'
  const tooltipBg = isDark ? 'rgba(15, 23, 42, 0.92)' : 'rgba(255, 255, 255, 0.92)'
  const tooltipBorder = isDark ? 'rgba(148, 163, 184, 0.15)' : 'rgba(0, 0, 0, 0.06)'
  const tooltipColor = isDark ? '#E2E8F0' : '#1C1917'

  if (chartMode.value === 'total') {
    return {
      tooltip: {
        trigger: 'axis',
        backgroundColor: tooltipBg,
        borderColor: tooltipBorder,
        borderWidth: 1,
        textStyle: { color: tooltipColor, fontSize: 13 },
        valueFormatter: (v: number) => `${v.toLocaleString()} 次`,
      },
      grid: { left: 40, right: 16, bottom: 24, top: 12 },
      xAxis: {
        type: 'category', data: categories,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: axisColor, fontSize: 11 },
      },
      yAxis: {
        type: 'value', min: 0,
        axisLine: { show: false },
        axisTick: { show: false },
        axisLabel: { color: axisColor, fontSize: 11 },
        splitLine: { lineStyle: { color: splitColor, type: 'dashed' } },
      },
      series: [{
        type: 'bar',
        data: data.total,
        barWidth: '32%',
        itemStyle: {
          color: makeBarGradient(brandColor.value),
          borderRadius: [6, 6, 0, 0],
        },
        animationDuration: 600,
        animationEasing: 'easeOutQuad',
      }],
    }
  }

  const series = chartMode.value === 'model' ? data.byModel : data.byKey
  const count = series.length
  return {
    tooltip: {
      trigger: 'axis',
      backgroundColor: tooltipBg,
      borderColor: tooltipBorder,
      borderWidth: 1,
      textStyle: { color: tooltipColor, fontSize: 13 },
      valueFormatter: (v: number) => `${v.toLocaleString()} 次`,
    },
    legend: {
      type: 'scroll', bottom: 0,
      textStyle: { color: isDark ? '#94A3B8' : '#78716C', fontSize: 12 },
    },
    grid: { left: 40, right: 16, bottom: 44, top: 12 },
    xAxis: {
      type: 'category', data: categories,
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: axisColor, fontSize: 11 },
    },
    yAxis: {
      type: 'value', min: 0,
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: axisColor, fontSize: 11 },
      splitLine: { lineStyle: { color: splitColor, type: 'dashed' } },
    },
    series: series.map((s, i) => ({
      name: s.name,
      type: 'bar',
      stack: 'total',
      data: s.data,
      barWidth: '44%',
      itemStyle: {
        color: makeBarGradient(colors.value[i % colors.value.length]),
        borderRadius: i === count - 1 ? [6, 6, 0, 0] : 0,
      },
      animationDuration: 400 + i * 80,
      animationEasing: 'easeOutQuad',
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
