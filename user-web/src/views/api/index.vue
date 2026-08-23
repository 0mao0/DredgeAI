<template>
  <div class="page-container">
    <PageHeader title="API 管理" description="管理 API Key、查看模型调用统计" />

    <a-tabs v-model:active-key="activeTab" class="api-tabs">
      <a-tab-pane key="keys" tab="API 管理">
        <DataTable
          :columns="columns"
          :data-source="apiKeys"
          :pagination="{ pageSize: 15 }"
          row-key="id"
        >
          <template #toolbarExtra>
            <AppButton variant="primary" @click="showCreateModal = true">
              <PlusOutlined />
              创建 Key
            </AppButton>
          </template>
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'key'">
              <code class="key-text">{{ record.key }}</code>
            </template>
            <template v-else-if="column.key === 'doc'">
              <AppButton variant="link" size="sm" @click="openDoc(record.docUrl)">
                <FileTextOutlined /> 文档
              </AppButton>
            </template>
            <template v-else-if="column.key === 'action'">
              <AppButton variant="link" size="sm" @click="handleEdit(record)">编辑</AppButton>
              <a-popconfirm title="确认删除？" @confirm="handleDelete(record.id)">
                <AppButton variant="link" size="sm" danger>删除</AppButton>
              </a-popconfirm>
            </template>
          </template>
        </DataTable>
      </a-tab-pane>

      <a-tab-pane key="calls" tab="调用记录">
        <DataTable
          v-model:query="callQuery"
          :columns="callColumns"
          :data-source="callRecords"
          :pagination="{ pageSize: 15 }"
          :filters="callFilters"
          row-key="id"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'userName'">
              <a-tooltip :title="`${record.department} · ${record.userName} · ${record.userPhone}`">
                <span>{{ record.userName }}</span>
              </a-tooltip>
            </template>
            <template v-else-if="column.key === 'latency'">
              {{ (record.latency ?? 0) }}ms
            </template>
            <template v-else-if="column.key === 'inputTokens'">
              {{ ((record.inputTokens ?? 0) / 10000).toFixed(1) }} 万
            </template>
            <template v-else-if="column.key === 'outputTokens'">
              {{ ((record.outputTokens ?? 0) / 10000).toFixed(1) }} 万
            </template>
            <template v-else-if="column.key === 'status'">
              <a-tag :color="record.status === '成功' ? 'green' : 'red'">{{ record.status }}</a-tag>
            </template>
          </template>
        </DataTable>
      </a-tab-pane>

      <a-tab-pane key="usage" tab="用量分析">
        <div class="stats-tab">
          <a-row :gutter="16" class="mb-24">
            <a-col :span="12">
              <MetricCard
                title="总调用次数"
                :value="formatNumber(usageTotalCalls)"
                suffix="次"
                icon="ThunderboltOutlined"
                :color="brandColor"
              />
            </a-col>
            <a-col :span="12">
              <MetricCard
                title="总 Token 消耗量"
                :value="formatNumber(usageTotalTokens)"
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

          <a-row :gutter="24" class="mt-24">
            <a-col :span="14" class="mb-24">
              <SectionCard title="模型消耗排名">
                <ChartContainer :option="modelRankingChartOption" height="340px" />
              </SectionCard>
            </a-col>
            <a-col :span="10" class="mb-24">
              <SectionCard title="模型用量占比">
                <ChartContainer :option="modelPieOption" height="340px" />
              </SectionCard>
            </a-col>
          </a-row>
        </div>
      </a-tab-pane>
    </a-tabs>

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
        <AppButton variant="primary" block @click="copyCreatedKey">
          <CopyOutlined /> 复制 Key
        </AppButton>
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
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
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
import { getApiKeyList, getUsageTimeSeries } from '@/api/modules/apikey'
import type { ApiKey, UsageTimeSeries } from '@/types'

const activeTab = ref('keys')

// ─── API Key 管理 ──────────────────────────────────────

const columns: DataTableColumn[] = [
  { title: '名称', dataIndex: 'name', key: 'name', width: 160, minWidth: 120, resizable: true },
  { title: 'Key', key: 'key', width: 240, minWidth: 180, resizable: true },
  { title: '模型', dataIndex: 'modelType', key: 'modelType', width: 120, minWidth: 100, resizable: true },
  { title: '创建日期', dataIndex: 'createdAt', key: 'createdAt', width: 120, minWidth: 110, resizable: true },
  { title: 'API 文档', key: 'doc', width: 110, minWidth: 90, resizable: true },
  { title: '操作', key: 'action', width: 130, minWidth: 130, fixed: 'right', resizable: true },
]

const apiKeys = ref<ApiKey[]>([])

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
const editTarget = ref<{ id: string, name: string } | null>(null)
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

const timeSeriesData = ref<UsageTimeSeries | null>(null)

onMounted(async () => {
  try {
    const [keys] = await Promise.all([
      getApiKeyList(),
    ])
    apiKeys.value = keys
  } catch {
    message.error('加载 API 数据失败')
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
    x: 0,
    y: 0,
    x2: 0,
    y2: 1,
    colorStops: [
      { offset: 0, color: hex },
      { offset: 1, color: `${hex}66` },
    ],
  }
}

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
      data.byModel.reduce((sum: number, m: { data: number[] }) => sum + (m.data[i] || 0), 0),
    ) || []
    return {
      ...base(),
      legend: undefined,
      series: [{ type: 'bar', data: totalData, barWidth: '32%', itemStyle: { color: makeBarGradient(brandColor.value), borderRadius: [6, 6, 0, 0] }, animationDuration: 600, animationEasing: 'easeOutQuad' }],
    }
  }

  const series = chartMode.value === 'model' ? data.byModel.map((m) => ({ name: m.modelName, data: m.data })) : data.byName
  const count = series.length
  return {
    ...base(),
    grid: { left: 40, right: 16, bottom: 44, top: 12 },
    legend: { type: 'scroll', bottom: 0, textStyle: { color: t.legendColor, fontSize: 12 } },
    series: series.map((s: { name: string, data: number[] }, i: number) => ({
      name: s.name,
      type: 'bar',
      stack: 'total',
      data: s.data,
      barWidth: '44%',
      itemStyle: { color: makeBarGradient(colors.value[i % colors.value.length]), borderRadius: i === count - 1 ? [6, 6, 0, 0] : 0 },
      animationDuration: 400 + i * 80,
      animationEasing: 'easeOutQuad',
    })),
  }
})

// ─── 调用记录 ───────────────────────────────────────────

const allModelNames = ['GPT-4o', 'GPT-4o-mini', 'Claude-3.5-Sonnet', 'DeepSeek-V3']

interface CallRecord {
  id: string
  userName: string
  userPhone: string
  department: string
  modelName: string
  inputTokens: number
  outputTokens: number
  latency: number
  status: '成功' | '失败'
  time: string
}

const callUserKeyword = ref('')
const callModelFilter = ref<string[]>([])
const callStatusFilter = ref<string | undefined>(undefined)

const mockCallRecords: CallRecord[] = (() => {
  const now = new Date()
  const names = ['张三', '李四', '王五', '赵六', '陈七', '刘八', '周九', '吴十']
  const depts = ['研发部', '产品部', '运营部', '市场部', '数据部']
  const phones = ['13800138001', '13900139002', '13700137003', '13600136004', '13500135005', '15800158006']
  const records: CallRecord[] = []
  for (let i = 0; i < 120; i++) {
    const d = new Date(now)
    d.setMinutes(d.getMinutes() - Math.floor(Math.random() * 4320))
    const userIdx = Math.floor(Math.random() * names.length)
    const modelNames = ['GPT-4o', 'GPT-4o-mini', 'Claude-3.5-Sonnet', 'DeepSeek-V3']
    const success = Math.random() > 0.15
    records.push({
      id: `call-${i}`,
      userName: names[userIdx],
      userPhone: phones[userIdx % phones.length],
      department: depts[userIdx % depts.length],
      modelName: modelNames[Math.floor(Math.random() * modelNames.length)],
      inputTokens: Math.round(100 + Math.random() * 4000),
      outputTokens: Math.round(100 + Math.random() * 4000),
      latency: success ? Math.round(300 + Math.random() * 5000) : Math.round(8000 + Math.random() * 12000),
      status: success ? '成功' : '失败',
      time: d.toISOString().slice(0, 19).replace('T', ' '),
    })
  }
  return records.sort((a, b) => b.time.localeCompare(a.time))
})()

const callColumns: DataTableColumn[] = [
  { title: '时间', dataIndex: 'time', key: 'time', width: 160, minWidth: 140, resizable: true },
  { title: '模型', dataIndex: 'modelName', key: 'modelName', width: 180, minWidth: 120, resizable: true },
  { title: '用户', key: 'userName', width: 100, minWidth: 90, resizable: true },
  { title: '输入 Token', key: 'inputTokens', width: 100, minWidth: 90, resizable: true },
  { title: '输出 Token', key: 'outputTokens', width: 100, minWidth: 90, resizable: true },
  { title: '延迟', key: 'latency', width: 90, minWidth: 80, resizable: true },
  { title: '状态', key: 'status', width: 80, minWidth: 70, resizable: true },
]

const callFilters: DataTableFilter[] = [
  { key: 'userKeyword', type: 'input', placeholder: '搜索用户', width: 180 },
  { key: 'modelFilter', type: 'select', multiple: true, placeholder: '模型', width: 160, options: allModelNames },
  { key: 'statusFilter', type: 'select', placeholder: '状态', width: 100, options: ['成功', '失败'] },
]

const callQuery = computed({
  get: () => ({ userKeyword: callUserKeyword.value, modelFilter: callModelFilter.value, statusFilter: callStatusFilter.value }),
  set: (v: { userKeyword: string, modelFilter: string[], statusFilter: string | undefined }) => {
    callUserKeyword.value = v.userKeyword
    callModelFilter.value = v.modelFilter
    callStatusFilter.value = v.statusFilter
  },
})

const callRecords = computed(() => {
  let list = mockCallRecords
  const kw = callUserKeyword.value.toLowerCase().trim()
  if (kw) list = list.filter((r) => r.userName.includes(kw))
  if (callModelFilter.value.length > 0 && callModelFilter.value.length < allModelNames.length) {
    list = list.filter((r) => callModelFilter.value.includes(r.modelName))
  }
  if (callStatusFilter.value) {
    list = list.filter((r) => r.status === callStatusFilter.value)
  }
  return list
})

// ─── 用量分析 ───────────────────────────────────────────

const usageTotalCalls = computed(() => {
  const data = timeSeriesData.value
  if (!data) return 0
  return data.byModel.reduce((sum: number, m) => sum + m.data.reduce((a: number, b: number) => a + b, 0), 0)
})
const usageTotalTokens = computed(() => Math.round(usageTotalCalls.value * 760))

const mockModels = [
  { id: '1', name: 'GPT-4o', consumption: 85600000 },
  { id: '2', name: 'GPT-4o-mini', consumption: 62300000 },
  { id: '3', name: 'Claude-3.5-Sonnet', consumption: 51200000 },
  { id: '4', name: 'DeepSeek-V3', consumption: 38700000 },
]

function formatConsumption(n: number): string {
  if (n >= 1e12) return `${(n / 1e12).toFixed(1)} 兆`
  if (n >= 1e8) return `${(n / 1e8).toFixed(1)} 亿`
  if (n >= 1e7) return `${(n / 1e7).toFixed(1)} 千万`
  if (n >= 1e4) return `${(n / 1e4).toFixed(1)} 万`
  return n.toLocaleString()
}

const modelRankingChartOption = computed(() => {
  const t = chartTheme()
  const sorted = [...mockModels].sort((a, b) => a.consumption - b.consumption)
  const names = sorted.map((m) => m.name)
  const vals = sorted.map((m) => m.consumption)
  const rankColors = [brandColor.value, accentColor.value, successColor.value, warningColor.value, dangerColor.value]
  return {
    tooltip: { trigger: 'axis' as const, backgroundColor: t.tooltipBg, borderColor: t.tooltipBorder, borderWidth: 1, textStyle: { color: t.tooltipColor, fontSize: 13 }, valueFormatter: (v: number) => formatConsumption(v) },
    grid: { left: 8, right: 60, bottom: 24, top: 8, containLabel: true },
    xAxis: {
      type: 'value' as const,
      name: '单位：千万',
      nameLocation: 'end' as const,
      nameGap: 6,
      nameTextStyle: { color: t.axisColor, fontSize: 11 },
      axisLine: { show: false },
      axisTick: { show: false },
      axisLabel: { color: t.axisColor, fontSize: 11, formatter: (v: number) => String(Math.round(v / 1e7)) },
      splitLine: { lineStyle: { color: t.splitColor, type: 'dashed' as const } },
    },
    yAxis: { type: 'category' as const, data: names, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 12 } },
    series: [{
      type: 'bar' as const,
      data: vals.map((v, i) => ({ value: v, itemStyle: { color: makeBarGradient(rankColors[i % rankColors.length]), borderRadius: [0, 4, 4, 0] } })),
      barWidth: '50%',
      label: { show: true, position: 'right' as const, color: t.axisColor, fontSize: 11, formatter: (p: { value: number }) => formatConsumption(p.value) },
      animationDuration: 600,
      animationEasing: 'easeOutQuad' as const,
    }],
  }
})

const modelPieOption = computed(() => {
  const t = chartTheme()
  return {
    tooltip: { trigger: 'item' as const, formatter: '{b}: {c} ({d}%)', backgroundColor: t.tooltipBg, borderColor: t.tooltipBorder, borderWidth: 1, textStyle: { color: t.tooltipColor, fontSize: 13 } },
    legend: { bottom: 0, type: 'scroll' as const, textStyle: { color: t.legendColor, fontSize: 12 } },
    series: [{
      type: 'pie' as const,
      radius: ['40%', '70%'],
      center: ['50%', '45%'],
      itemStyle: { borderRadius: 6, borderColor: 'transparent', borderWidth: 2 },
      label: { show: false },
      emphasis: { label: { show: true, fontSize: 14, fontWeight: 'bold' as const } },
      data: mockModels.map((m) => ({ name: m.name, value: m.consumption })),
      color: colors.value,
      animationDuration: 600,
    }],
  }
})

function openDoc(url: string): void {
  window.open(url, '_blank')
}

function handleDelete(id: string): void {
  apiKeys.value = apiKeys.value.filter((k) => k.id !== id)
  message.success('已删除')
}

function handleEdit(record: { id: string, name: string }): void {
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
  createdKey.value = `sk-dg-${Array.from({ length: 20 }, () => 'abcdefghijklmnopqrstuvwxyz0123456789'[Math.floor(Math.random() * 36)]).join('')}`
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
.mt-24 { margin-top: @spacing-xl; }

.api-tabs {
  :deep(.ant-tabs-nav) { margin-bottom: @spacing-sm; }
  :deep(.ant-tabs-nav-wrap) { padding: 0; }
  :deep(.ant-tabs-nav-list) { gap: 0; }
  :deep(.ant-tabs-tab) { padding: 4px 10px; }
  :deep(.ant-tabs-tab + .ant-tabs-tab) { margin-left: @spacing-md; }
  :deep(.ant-tabs-content-holder) { margin-top: 0; }
}
.page-container :deep(.page-header-left) {
  display: flex;
  align-items: baseline;
  gap: @spacing-sm;
}
.page-container :deep(.page-desc) {
  margin-top: 0;
  color: @text-tertiary;
}
.stats-tab :deep(.section-card-header) {
  padding: @spacing-md @spacing-xl;
}
.stats-tab :deep(.section-card-body) {
  padding: @spacing-md @spacing-xl;
}
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
