<template>
  <div class="page-container">
    <PageHeader title="API 管理" description="管理接入的模型、统计平台用量与配置用户限制">
      <template #extra>
        <a-button type="primary" @click="showCreateModal = true">
          <plus-outlined />
          添加模型
        </a-button>
      </template>
    </PageHeader>

    <a-tabs v-model:activeKey="activeTab" class="api-tabs">
      <a-tab-pane key="keys" tab="模型管理">
        <SectionCard nopad>
          <a-table
            :data-source="models"
            :columns="modelColumns"
            :pagination="{ pageSize: 10 }"
            row-key="id"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.key === 'index'">
                {{ index + 1 }}
              </template>
              <template v-else-if="column.key === 'status'">
                <a-tag :color="record.status === '启用' ? 'green' : 'default'">{{ record.status }}</a-tag>
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

      </a-tab-pane>

      <a-tab-pane key="stats" tab="统计总览">
        <a-row :gutter="16" class="mb-24">
          <a-col :span="12">
            <MetricCard
              title="总调用次数"
              :value="formatNumber(overviewTotalCalls)"
              suffix="次"
              icon="ThunderboltOutlined"
              :color="brandColor"
            />
          </a-col>
          <a-col :span="12">
            <MetricCard
              title="总 Token 消耗量"
              :value="formatNumber(overviewTotalTokens)"
              suffix="tokens"
              icon="DatabaseOutlined"
              :color="accentColor"
            />
          </a-col>
        </a-row>

        <SectionCard title="全平台调用趋势">
          <div class="chart-header">
            <a-radio-group v-model:value="overviewChartMode" size="small">
              <a-radio-button value="model">按模型</a-radio-button>
              <a-radio-button value="key">按 API Key</a-radio-button>
              <a-radio-button value="total">调用次数</a-radio-button>
            </a-radio-group>
            <div class="time-range-wrap">
              <a-radio-group v-model:value="overviewTimeRange" size="small">
                <a-radio-button value="7d">近7日</a-radio-button>
                <a-radio-button value="30d">近30日</a-radio-button>
                <a-radio-button value="month">本月</a-radio-button>
                <a-radio-button value="prevMonth">上月</a-radio-button>
                <a-radio-button value="custom">自定义</a-radio-button>
              </a-radio-group>
              <a-range-picker
                v-if="overviewTimeRange === 'custom'"
                v-model:value="overviewCustomDateRange"
                size="small"
                class="custom-date-picker"
                :allow-empty="false"
              />
            </div>
          </div>
          <ChartContainer :option="overviewChartOption" height="320px" />
        </SectionCard>

        <a-row :gutter="24" class="mt-24">
          <a-col :span="14" class="mb-24">
            <SectionCard title="用户调用排名">
              <ChartContainer :option="userRankingChartOption" height="340px" />
            </SectionCard>
          </a-col>
          <a-col :span="10" class="mb-24">
            <SectionCard title="模型用量占比">
              <ChartContainer :option="modelPieOption" height="340px" />
            </SectionCard>
          </a-col>
        </a-row>
      </a-tab-pane>

      <a-tab-pane key="ranking" tab="用户控制">
        <SectionCard nopad>
          <a-table
            :data-source="mergedUserData"
            :columns="mergedUserColumns"
            :pagination="{ pageSize: 10 }"
            row-key="userId"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.key === 'rank'">
                <span :class="['rank-badge', { gold: index < 3 }]">{{ index + 1 }}</span>
              </template>
              <template v-else-if="column.key === 'calls'">
                 {{ formatNumber(record.calls) }}
              </template>
              <template v-else-if="column.key === 'tokens'">
                 {{ formatNumber(record.tokens) }}
              </template>
              <template v-else-if="column.key === 'models'">
                <a-tag :color="record.models.length === 4 ? 'green' : 'orange'">{{ record.models.length === 4 ? '全部' : '部分' }}</a-tag>
              </template>
              <template v-else-if="column.key === 'action'">
                <a-button type="link" size="small" @click="handleEditLimits(record)">编辑限制</a-button>
              </template>
            </template>
          </a-table>
        </SectionCard>
      </a-tab-pane>
    </a-tabs>

    <!-- Create Modal -->
    <a-modal v-model:open="showCreateModal" title="添加模型" @ok="handleCreate" @cancel="resetNewModel()">
      <a-form layout="vertical">
        <a-form-item label="模型类型" required>
          <a-select v-model:value="newModel.modelType" :options="modelTypeOptions" placeholder="选择模型提供商" />
        </a-form-item>
        <a-form-item label="模型名称" required>
          <a-input v-model:value="newModel.name" placeholder="如：GPT-4o" />
        </a-form-item>
        <a-form-item label="状态">
          <a-select v-model:value="newModel.status" :options="statusOptions" />
        </a-form-item>
        <a-form-item label="API 文档链接">
          <a-input v-model:value="newModel.docUrl" placeholder="https://docs.example.com/model" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- Edit Modal -->
    <a-modal v-model:open="showEditModal" title="编辑模型" @ok="handleEditOk">
      <a-form layout="vertical" v-if="editTarget">
        <a-form-item label="模型类型" required>
          <a-select v-model:value="editForm.modelType" :options="modelTypeOptions" placeholder="选择模型提供商" />
        </a-form-item>
        <a-form-item label="模型名称" required>
          <a-input v-model:value="editForm.name" placeholder="如：GPT-4o" />
        </a-form-item>
        <a-form-item label="状态">
          <a-select v-model:value="editForm.status" :options="statusOptions" />
        </a-form-item>
        <a-form-item label="API 文档链接">
          <a-input v-model:value="editForm.docUrl" placeholder="https://docs.example.com/model" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- Edit Limits Modal -->
    <a-modal v-model:open="showLimitsModal" title="编辑用户限制" width="600px" @ok="handleLimitsOk">
      <a-form layout="vertical" v-if="limitsTarget">
        <a-form-item label="用户">
          <span class="limits-user-name">{{ limitsTarget.name }}</span>
        </a-form-item>
        <a-form-item label="允许的模型">
          <a-select v-model:value="limitsForm.models" mode="multiple" :options="modelOptions" placeholder="默认全选" />
        </a-form-item>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="月调用次数限制">
              <a-input-number v-model:value="limitsForm.monthCallsLimit" :min="0" style="width:100%" placeholder="0=无限制" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="月调用次数预警">
              <a-input-number v-model:value="limitsForm.monthCallsWarn" :min="0" style="width:100%" placeholder="0=不预警" />
            </a-form-item>
          </a-col>
        </a-row>
        <a-row :gutter="16">
          <a-col :span="12">
            <a-form-item label="月 Token 总量限制">
              <a-input-number v-model:value="limitsForm.monthTokensLimit" :min="0" style="width:100%" placeholder="0=无限制" />
            </a-form-item>
          </a-col>
          <a-col :span="12">
            <a-form-item label="月 Token 总量预警">
              <a-input-number v-model:value="limitsForm.monthTokensWarn" :min="0" style="width:100%" placeholder="0=不预警" />
            </a-form-item>
          </a-col>
        </a-row>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, FileTextOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/components/PageHeader.vue'
import SectionCard from '@shared/components/SectionCard.vue'
import ChartContainer from '@shared/components/ChartContainer.vue'
import MetricCard from '@shared/components/MetricCard.vue'
import { formatNumber } from '@shared/core/utils/format'
import { useCssVar } from '@shared/composables/useCssVar'

const activeTab = ref('keys')

// ─── Mock Data ────────────────────────────────────────

const mockModels = [
  { id: '1', modelType: '文本对话', name: 'GPT-4o', status: '启用', createdAt: '2026-06-01', docUrl: 'https://docs.example.com/gpt4o' },
  { id: '2', modelType: '文本对话', name: 'GPT-4o-mini', status: '启用', createdAt: '2026-06-10', docUrl: 'https://docs.example.com/gpt4o-mini' },
  { id: '3', modelType: '多模态', name: 'Claude-3.5-Sonnet', status: '未启用', createdAt: '2026-06-15', docUrl: 'https://docs.example.com/claude35' },
  { id: '4', modelType: '文本对话', name: 'DeepSeek-V3', status: '启用', createdAt: '2026-06-20', docUrl: 'https://docs.example.com/deepseek' },
  { id: '5', modelType: '图像生成', name: 'DALL-E 3', status: '启用', createdAt: '2026-07-01', docUrl: 'https://docs.example.com/dalle3' },
  { id: '6', modelType: '语音合成', name: 'TTS-1', status: '未启用', createdAt: '2026-07-05', docUrl: 'https://docs.example.com/tts1' },
  { id: '7', modelType: '图像识别', name: 'GPT-4V', status: '启用', createdAt: '2026-07-08', docUrl: 'https://docs.example.com/gpt4v' },
]

const models = ref(mockModels)

const modelTypeOptions = [
  { label: '文本对话', value: '文本对话' },
  { label: '多模态', value: '多模态' },
  { label: '图像生成', value: '图像生成' },
  { label: '图像识别', value: '图像识别' },
  { label: '语音合成', value: '语音合成' },
]

const statusOptions = [
  { label: '启用', value: '启用' },
  { label: '未启用', value: '未启用' },
]

const modelOptions = [
  { label: 'GPT-4o', value: 'GPT-4o' },
  { label: 'GPT-4o-mini', value: 'GPT-4o-mini' },
  { label: 'Claude-3.5-Sonnet', value: 'Claude-3.5-Sonnet' },
  { label: 'DeepSeek-V3', value: 'DeepSeek-V3' },
]

// ─── Columns ──────────────────────────────────────────

const modelColumns = [
  { title: '序号', key: 'index', width: 70, align: 'center' },
  { title: '模型类型', dataIndex: 'modelType', key: 'modelType', align: 'center' },
  { title: '模型名称', dataIndex: 'name', key: 'name', align: 'center' },
  { title: '状态', key: 'status', width: 100, align: 'center' },
  { title: '创建日期', dataIndex: 'createdAt', key: 'createdAt', width: 120, align: 'center' },
  { title: 'API 文档', key: 'doc', width: 110, align: 'center' },
  { title: '操作', key: 'action', width: 130, align: 'center' },
]

const mergedUserColumns = [
  { title: '排名', key: 'rank', width: 70, align: 'center' },
  { title: '用户', dataIndex: 'name', key: 'name', align: 'center' },
  { title: '部门', dataIndex: 'department', key: 'department', align: 'center' },
  { title: '总调用次数', key: 'calls', align: 'center', sorter: (a: any, b: any) => a.calls - b.calls, sortDirections: ['ascend', 'descend'] },
  { title: '总 Token 用量', key: 'tokens', align: 'center', sorter: (a: any, b: any) => a.tokens - b.tokens, sortDirections: ['ascend', 'descend'] },
  { title: '授权模型', key: 'models', width: 100, align: 'center' },
  { title: '操作', key: 'action', width: 110, align: 'center' },
]

// ─── CRUD State ───────────────────────────────────────

const showCreateModal = ref(false)
const showEditModal = ref(false)
const showLimitsModal = ref(false)

function emptyModel() {
  return { modelType: '', name: '', status: '启用', docUrl: '' }
}

const newModel = ref(emptyModel())
const editTarget = ref<{ id: string } | null>(null)
const editForm = ref(emptyModel())
const limitsTarget = ref<{ userId: string; name: string; models: string[]; monthCallsLimit: number; monthCallsWarn: number; monthTokensLimit: number; monthTokensWarn: number } | null>(null)
const limitsForm = ref({
  models: [] as string[],
  monthCallsLimit: 0,
  monthCallsWarn: 0,
  monthTokensLimit: 0,
  monthTokensWarn: 0,
})

const brandColor = useCssVar('--color-brand')
const successColor = useCssVar('--color-success')
const accentColor = useCssVar('--color-accent')
const warningColor = useCssVar('--color-warning')
const dangerColor = useCssVar('--color-danger')

const colors = computed(() => [brandColor.value, accentColor.value, successColor.value, warningColor.value, dangerColor.value])

// ─── Chart Helpers ────────────────────────────────────

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

// ─── Tab 2: Charts ────────────────────────────────────

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

const overviewChartMode = ref<'model' | 'key' | 'total'>('model')
const overviewTimeRange = ref('7d')
const overviewCustomDateRange = ref()

const overviewDays30 = generateDays(30)
const overviewDays7 = generateDays(7)
const overviewDaysMonth = computed(() => generateDays(new Date().getDate()))
const overviewDaysPrevMonth = computed(() => {
  const now = new Date()
  return generateDays(daysInMonth(now.getFullYear(), now.getMonth()), -daysInMonth(now.getFullYear(), now.getMonth()))
})

const overviewChartData = computed(() => {
  let days: string[]
  switch (overviewTimeRange.value) {
    case '7d': days = overviewDays7; break
    case 'month': days = overviewDaysMonth.value; break
    case 'prevMonth': days = overviewDaysPrevMonth.value; break
    default: days = overviewDays30
  }
  const len = days.length
  return {
    categories: days,
    byModel: [
      { name: 'GPT-4o', data: makeMockSeries(1200, 600, len) },
      { name: 'GPT-4o-mini', data: makeMockSeries(1800, 800, len) },
      { name: 'Claude-3.5-Sonnet', data: makeMockSeries(600, 300, len) },
      { name: 'DeepSeek-V3', data: makeMockSeries(900, 400, len) },
    ],
    byKey: [
      { name: '生产-主', data: makeMockSeries(450, 250, len) },
      { name: '生产-备', data: makeMockSeries(200, 150, len) },
      { name: '测试环境', data: makeMockSeries(380, 200, len) },
      { name: '内部工具', data: makeMockSeries(160, 100, len) },
      { name: '数据分析', data: makeMockSeries(140, 80, len) },
      { name: '移动端', data: makeMockSeries(280, 160, len) },
      { name: '批量任务', data: makeMockSeries(100, 60, len) },
    ],
    total: makeMockSeries(4500, 1200, len),
  }
})

const overviewTotalCalls = computed(() => {
  const data = overviewChartData.value
  if (overviewChartMode.value === 'total') {
    return data.total.reduce((s, v) => s + v, 0)
  }
  const series = overviewChartMode.value === 'model' ? data.byModel : data.byKey
  return series.reduce((sum, s) => sum + s.data.reduce((a, b) => a + b, 0), 0)
})

const overviewTokensPerCall = computed(() => {
  const data = overviewChartData.value
  if (overviewChartMode.value === 'total') return 760
  const series = overviewChartMode.value === 'model' ? data.byModel : data.byKey
  const weights = series.map((s) =>
    s.name.includes('mini') || s.name.includes('DeepSeek') || s.name.includes('测试')
      ? 300
      : s.name.includes('GPT-4o') || s.name.includes('生产')
        ? 980
        : 680,
  )
  const totals = series.map((s) => s.data.reduce((a, b) => a + b, 0))
  const sumCalls = totals.reduce((a, b) => a + b, 0)
  if (sumCalls === 0) return 760
  const sumTokens = totals.reduce((a, b, i) => a + b * weights[i], 0)
  return sumTokens / sumCalls
})

const overviewTotalTokens = computed(() => Math.round(overviewTotalCalls.value * overviewTokensPerCall.value))

const overviewChartOption = computed(() => {
  const data = overviewChartData.value
  const categories = data.categories
  const isDark = document.documentElement.getAttribute('data-theme') === 'dark'
  const axisColor = isDark ? '#52627A' : '#A8A29E'
  const splitColor = isDark ? 'rgba(148, 163, 184, 0.08)' : 'rgba(0, 0, 0, 0.06)'
  const tooltipBg = isDark ? 'rgba(15, 23, 42, 0.92)' : 'rgba(255, 255, 255, 0.92)'
  const tooltipBorder = isDark ? 'rgba(148, 163, 184, 0.15)' : 'rgba(0, 0, 0, 0.06)'
  const tooltipColor = isDark ? '#E2E8F0' : '#1C1917'

  const base = {
    tooltip: { trigger: 'axis' as const, backgroundColor: tooltipBg, borderColor: tooltipBorder, borderWidth: 1, textStyle: { color: tooltipColor, fontSize: 13 }, valueFormatter: (v: number) => `${v.toLocaleString()} 次` },
    grid: { left: 40, right: 16, bottom: 44, top: 12 },
    xAxis: { type: 'category' as const, data: categories, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: axisColor, fontSize: 11 } },
    yAxis: { type: 'value' as const, min: 0, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: axisColor, fontSize: 11 }, splitLine: { lineStyle: { color: splitColor, type: 'dashed' as const } } },
    legend: { type: 'scroll' as const, bottom: 0, textStyle: { color: isDark ? '#94A3B8' : '#78716C', fontSize: 12 } },
  }

  if (overviewChartMode.value === 'total') {
    return {
      ...base, legend: undefined,
      grid: { left: 40, right: 16, bottom: 24, top: 12 },
      series: [{ type: 'bar' as const, data: data.total, barWidth: '32%', itemStyle: { color: makeBarGradient(brandColor.value), borderRadius: [6, 6, 0, 0] }, animationDuration: 600, animationEasing: 'easeOutQuad' as const }],
    }
  }

  const series = overviewChartMode.value === 'model' ? data.byModel : data.byKey
  return {
    ...base,
    series: series.map((s, i) => ({
      name: s.name, type: 'bar' as const, stack: 'total', data: s.data, barWidth: '44%',
      itemStyle: { color: makeBarGradient(colors.value[i % colors.value.length]), borderRadius: i === series.length - 1 ? [6, 6, 0, 0] : 0 },
      animationDuration: 400 + i * 80, animationEasing: 'easeOutQuad' as const,
    })),
  }
})

const userRankingChartOption = computed(() => {
  const t = chartTheme()
  const users = ['冯乙', '郑甲', '吴十', '周九', '刘八', '陈七', '王五', '张三', '赵六', '李四']
  const vals = [28000, 53000, 86000, 118000, 152000, 196000, 241000, 285000, 328000, 412000]
  const rankColors = [brandColor.value, accentColor.value, successColor.value, warningColor.value, dangerColor.value]
  return {
    tooltip: { trigger: 'axis' as const, backgroundColor: t.tooltipBg, borderColor: t.tooltipBorder, borderWidth: 1, textStyle: { color: t.tooltipColor, fontSize: 13 }, valueFormatter: (v: number) => `${v.toLocaleString()} 次` },
    grid: { left: 60, right: 40, bottom: 16, top: 12 },
    xAxis: { type: 'value' as const, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11 }, splitLine: { lineStyle: { color: t.splitColor, type: 'dashed' as const } } },
    yAxis: { type: 'category' as const, data: users, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11 } },
    series: [{
      type: 'bar' as const, data: vals.map((v, i) => ({ value: v, itemStyle: { color: makeBarGradient(rankColors[i % rankColors.length]), borderRadius: [0, 6, 6, 0] } })),
      barWidth: '55%', animationDuration: 600, animationEasing: 'easeOutQuad' as const,
    }],
  }
})

const modelPieOption = computed(() => {
  const t = chartTheme()
  return {
    tooltip: { trigger: 'item' as const, formatter: '{b}: {c} ({d}%)', backgroundColor: t.tooltipBg, borderColor: t.tooltipBorder, borderWidth: 1, textStyle: { color: t.tooltipColor, fontSize: 13 } },
    legend: { bottom: 0, type: 'scroll' as const, textStyle: { color: t.legendColor, fontSize: 12 } },
    series: [{
      type: 'pie' as const, radius: ['40%', '70%'], center: ['50%', '45%'],
      itemStyle: { borderRadius: 6, borderColor: 'transparent', borderWidth: 2 },
      label: { show: false },
      emphasis: { label: { show: true, fontSize: 14, fontWeight: 'bold' as const } },
      data: [
        { name: 'GPT-4o-mini', value: 478000 },
        { name: 'GPT-4o', value: 452000 },
        { name: 'DeepSeek-V3', value: 318000 },
        { name: 'Claude-3.5-Sonnet', value: 195000 },
      ],
      color: colors.value,
      animationDuration: 600,
    }],
  }
})

// ─── Tab 3: User Rankings ─────────────────────────────

const mergedUserData = computed(() => [
  { userId: 'u2', name: '李四', department: '研发部', calls: 412000, tokens: 85600000, models: ['GPT-4o', 'GPT-4o-mini', 'Claude-3.5-Sonnet', 'DeepSeek-V3'], monthCallsLimit: 0, monthCallsWarn: 0, monthTokensLimit: 0, monthTokensWarn: 0 },
  { userId: 'u4', name: '赵六', department: '产品部', calls: 328000, tokens: 62300000, models: ['GPT-4o', 'DeepSeek-V3'], monthCallsLimit: 0, monthCallsWarn: 0, monthTokensLimit: 0, monthTokensWarn: 0 },
  { userId: 'u1', name: '张三', department: '运营部', calls: 285000, tokens: 51200000, models: ['GPT-4o', 'GPT-4o-mini'], monthCallsLimit: 50000, monthCallsWarn: 40000, monthTokensLimit: 10000000, monthTokensWarn: 8000000 },
  { userId: 'u5', name: '陈七', department: '市场部', calls: 196000, tokens: 38700000, models: ['GPT-4o-mini', 'Claude-3.5-Sonnet', 'DeepSeek-V3'], monthCallsLimit: 200000, monthCallsWarn: 150000, monthTokensLimit: 100000000, monthTokensWarn: 80000000 },
  { userId: 'u3', name: '王五', department: '数据部', calls: 156000, tokens: 29500000, models: ['DeepSeek-V3'], monthCallsLimit: 100000, monthCallsWarn: 80000, monthTokensLimit: 50000000, monthTokensWarn: 40000000 },
])

// ─── CRUD Handlers ────────────────────────────────────

function openDoc(url: string): void {
  window.open(url, '_blank')
}

function handleDelete(id: string): void {
  models.value = models.value.filter((m) => m.id !== id)
  message.success('已删除')
}

function handleEdit(record: { id: string; modelType: string; name: string; status: string; docUrl: string }): void {
  editTarget.value = record
  editForm.value = { modelType: record.modelType, name: record.name, status: record.status, docUrl: record.docUrl }
  showEditModal.value = true
}

function handleEditOk(): void {
  if (!editForm.value.name || !editForm.value.modelType) {
    message.warning('请填写完整信息')
    return
  }
  if (editTarget.value) {
    const item = models.value.find((m) => m.id === editTarget.value!.id)
    if (item) {
      item.modelType = editForm.value.modelType
      item.name = editForm.value.name
      item.status = editForm.value.status
      item.docUrl = editForm.value.docUrl
    }
  }
  message.success('已更新')
  showEditModal.value = false
  editTarget.value = null
}

function handleCreate(): void {
  if (!newModel.value.name || !newModel.value.modelType) {
    message.warning('请填写完整信息')
    return
  }
  const id = String(Date.now())
  models.value.unshift({ id, ...newModel.value, createdAt: new Date().toISOString().slice(0, 10) })
  message.success('模型已添加')
  showCreateModal.value = false
  newModel.value = emptyModel()
}

function resetNewModel(): void {
  newModel.value = emptyModel()
}

function handleEditLimits(user: { userId: string; name: string; models: string[]; monthCallsLimit: number; monthCallsWarn: number; monthTokensLimit: number; monthTokensWarn: number }): void {
  limitsTarget.value = user
  limitsForm.value = {
    models: [...user.models],
    monthCallsLimit: user.monthCallsLimit,
    monthCallsWarn: user.monthCallsWarn,
    monthTokensLimit: user.monthTokensLimit,
    monthTokensWarn: user.monthTokensWarn,
  }
  showLimitsModal.value = true
}

function handleLimitsOk(): void {
  if (limitsTarget.value) {
    Object.assign(limitsTarget.value, {
      models: limitsForm.value.models,
      monthCallsLimit: limitsForm.value.monthCallsLimit,
      monthCallsWarn: limitsForm.value.monthCallsWarn,
      monthTokensLimit: limitsForm.value.monthTokensLimit,
      monthTokensWarn: limitsForm.value.monthTokensWarn,
    })
    message.success('限制已更新')
  }
  showLimitsModal.value = false
  limitsTarget.value = null
}
</script>

<style scoped lang="less">
@import '@shared/styles/variables.less';

.mb-24 { margin-bottom: @spacing-xl; }
.mt-24 { margin-top: @spacing-xl; }

.api-tabs {
  :deep(.ant-tabs-nav) { margin-bottom: @spacing-xl; }
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

.stat-card {
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-lg;
  padding: @spacing-lg @spacing-xl;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.stat-label {
  font-size: @font-size-sm;
  color: @text-secondary;
}
.stat-value {
  font-size: @font-size-2xl;
  font-weight: @font-weight-bold;
  color: @text-primary;
  line-height: 1.2;
}
.stat-sub {
  font-size: @font-size-xs;
  color: @text-tertiary;
  margin-top: 2px;
}

.rank-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 24px;
  height: 24px;
  border-radius: 50%;
  font-size: @font-size-xs;
  font-weight: @font-weight-semibold;
  background: @content-bg;
  color: @text-secondary;
  &.gold {
    background: linear-gradient(135deg, #f59e0b, #d97706);
    color: #fff;
  }
}

.model-tag {
  margin-bottom: 4px;
}

.limits-user-name {
  font-weight: @font-weight-semibold;
  color: @text-primary;
}
</style>
