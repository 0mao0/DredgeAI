<template>
  <div class="page-container" :class="{ 'api-page--keys': activeTab === 'keys', 'api-page--alerts': activeTab === 'alerts', 'api-page--permissions': activeTab === 'permissions', 'api-page--calls': activeTab === 'calls' }">
    <PageHeader title="API 管理" description="管理接入的模型、统计平台用量与配置用户限制">
      <template #extra>
        <AppButton v-if="activeTab === 'keys'" variant="primary" size="sm" @click="showCreateModal = true">
          <PlusOutlined />
          添加模型
        </AppButton>
        <a-radio-group v-if="activeTab === 'alerts'" v-model:value="alertFilter" size="small" button-style="solid">
          <a-radio-button value="all">全部</a-radio-button>
          <a-radio-button value="calls">调用超限</a-radio-button>
          <a-radio-button value="tokens">Token 超限</a-radio-button>
        </a-radio-group>
      </template>
    </PageHeader>

    <a-tabs v-model:active-key="activeTab" class="api-tabs" :class="{ 'api-tabs--keys': activeTab === 'keys', 'api-tabs--alerts': activeTab === 'alerts', 'api-tabs--permissions': activeTab === 'permissions', 'api-tabs--calls': activeTab === 'calls', 'api-tabs--usage': activeTab === 'usage' }">
      <template #tabBarExtraContent>
        <a-space v-if="activeTab === 'keys'" :size="8">
          <AppButton variant="primary" size="sm" @click="showCreateModal = true">
            <PlusOutlined />
            添加模型
          </AppButton>
        </a-space>
        <a-space v-else-if="activeTab === 'calls'" :size="8">
          <a-input-search v-model:value="callUserKeyword" placeholder="搜索用户" allow-clear size="small" style="width:180px" />
          <a-select v-model:value="callModelFilter" mode="multiple" allow-clear placeholder="模型" size="small" :max-tag-count="0" :max-tag-placeholder="callModelFilter.length ? `已选 ${callModelFilter.length}` : '全部'" style="width:140px">
            <a-select-option v-for="m in allModelNames" :key="m" :value="m">{{ m }}</a-select-option>
          </a-select>
          <a-select v-model:value="callStatusFilter" allow-clear placeholder="状态" style="width:100px">
            <a-select-option value="成功">成功</a-select-option>
            <a-select-option value="失败">失败</a-select-option>
          </a-select>
        </a-space>
        <a-space v-else-if="activeTab === 'permissions'" :size="8">
          <a-input-search v-model:value="permissionKeyword" placeholder="搜索姓名 / 部门" allow-clear size="small" style="width:200px" />
          <a-switch v-model:checked="partialOnly" checked-children="部分权限" un-checked-children="全部权限" size="small" />
        </a-space>
        <a-space v-else-if="activeTab === 'alerts'" :size="8">
          <a-radio-group v-model:value="alertFilter" size="small" button-style="solid">
            <a-radio-button value="all">全部</a-radio-button>
            <a-radio-button value="calls">调用超限</a-radio-button>
            <a-radio-button value="tokens">Token 超限</a-radio-button>
          </a-radio-group>
        </a-space>
        <a-space v-else-if="activeTab === 'usage'" :size="8">
          <a-segmented v-model:value="usageDimension" :options="['模型维度', '用户维度']" />
          <template v-if="usageDimension === '用户维度'">
            <a-input-search v-model:value="userKeyword" placeholder="搜索姓名 / 部门" allow-clear size="small" style="width:200px" />
            <a-select v-model:value="userDepartment" allow-clear placeholder="部门" style="width:140px">
              <a-select-option v-for="d in allDepartments" :key="d" :value="d">{{ d }}</a-select-option>
            </a-select>
            <a-select v-model:value="userModel" mode="multiple" allow-clear placeholder="全部" :max-tag-count="0" :max-tag-placeholder="userModel.length === 0 || userModel.length === allModelNames.length ? '全部' : `已选 ${userModel.length} 项`" style="width:140px">
              <a-select-option v-for="m in allModelNames" :key="m" :value="m">{{ m }}</a-select-option>
            </a-select>
          </template>
        </a-space>
      </template>

      <a-tab-pane key="keys" tab="模型管理">
        <KeysTab
          v-model:create-open="showCreateModal"
          v-model:edit-open="showEditModal"
          v-model:new-model="newModel"
          v-model:edit-form="editForm"
          :models="models"
          :edit-target="editTarget"
          :deployed-model-options="deployedModelOptions"
          :model-type-options="modelTypeOptions"
          :status-options="statusOptions"
          @open-create="showCreateModal = true"
          @create="handleCreate"
          @edit-ok="handleEditOk"
          @cancel-create="resetNewModel()"
          @edit="handleEdit"
          @delete="handleDelete"
        />
      </a-tab-pane>

      <a-tab-pane key="calls" tab="调用记录">
        <CallsTab
          v-model:user-keyword="callUserKeyword"
          v-model:model-filter="callModelFilter"
          v-model:status-filter="callStatusFilter"
          :records="filteredCallRecords"
          :loading="callRecordsLoading"
          :all-model-names="allModelNames"
        />
      </a-tab-pane>

      <a-tab-pane key="usage" tab="用量分析">
        <UsageTab
          v-model:usage-dimension="usageDimension"
          v-model:chart-mode="overviewChartMode"
          v-model:time-range="overviewTimeRange"
          v-model:custom-date-range="overviewCustomDateRange"
          v-model:user-keyword="userKeyword"
          v-model:user-department="userDepartment"
          v-model:user-model="userModel"
          :overview-total-calls="overviewTotalCalls"
          :overview-total-tokens="overviewTotalTokens"
          :overview-chart-option="overviewChartOption"
          :user-ranking-chart-option="userRankingChartOption"
          :model-pie-option="modelPieOption"
          :all-departments="allDepartments"
          :all-model-names="allModelNames"
          :merged-user-data="mergedUserData"
        />
      </a-tab-pane>

      <a-tab-pane key="permissions" tab="权限控制">
        <PermissionsTab
          v-model:keyword="permissionKeyword"
          v-model:partial-only="partialOnly"
          v-model:limits-open="showLimitsModal"
          v-model:limits-form="limitsForm"
          :users="permissionUsers"
          :all-model-names="allModelNames"
          :limits-target="limitsTarget"
          @edit-limits="handleEditLimits"
          @limits-ok="handleLimitsOk"
        />
      </a-tab-pane>

      <a-tab-pane key="alerts" tab="告警管理">
        <AlertsTab
          v-model:filter="alertFilter"
          :alerts="alertData"
        />
      </a-tab-pane>
    </a-tabs>
  </div>
</template>

<script setup lang="ts">
import { AppButton } from '@shared/web'
import { ref, computed, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import { useCssVar } from '@shared/web/composables/useCssVar'
import { useChartTheme } from '@shared/web/composables/useChartTheme'
import { getUsageRecords, getUsageTimeSeries } from '@/api/modules/apikey'
import type { UsageTimeSeries } from '@/types'
import KeysTab from './components/KeysTab.vue'
import CallsTab from './components/CallsTab.vue'
import UsageTab from './components/UsageTab.vue'
import PermissionsTab from './components/PermissionsTab.vue'
import AlertsTab from './components/AlertsTab.vue'
import { formatConsumption } from './utils'
import type { ModelItem, ModelLimitEntry, MergedUserRecord, CallRecord, AlertRecord, DayjsLike } from './types'

const { chartTheme } = useChartTheme()

const activeTab = ref('keys')
const usageDimension = ref('模型维度')

// ─── Models (CRUD local state, no backend API) ──────────

const deployedModelOptions = [
  { label: 'GPT-4o', value: 'GPT-4o' },
  { label: 'GPT-4o-mini', value: 'GPT-4o-mini' },
  { label: 'Claude-3.5-Sonnet', value: 'Claude-3.5-Sonnet' },
  { label: 'DeepSeek-V3', value: 'DeepSeek-V3' },
  { label: 'DALL-E 3', value: 'DALL-E 3' },
  { label: 'TTS-1', value: 'TTS-1' },
  { label: 'GPT-4V', value: 'GPT-4V' },
]

const mockModels: ModelItem[] = [
  { id: '1', name: '智能对话主模型', actualModel: 'GPT-4o', modelType: '文本对话', ipAddress: '192.168.1.10', docUrl: 'https://docs.example.com/gpt4o', status: '启用', createdAt: '2026-06-01', consumption: 85600000 },
  { id: '2', name: '轻量对话模型', actualModel: 'GPT-4o-mini', modelType: '文本对话', ipAddress: '192.168.1.11', docUrl: 'https://docs.example.com/gpt4o-mini', status: '启用', createdAt: '2026-06-10', consumption: 62300000 },
  { id: '3', name: '多模态分析模型', actualModel: 'Claude-3.5-Sonnet', modelType: '多模态', ipAddress: '192.168.1.12', docUrl: 'https://docs.example.com/claude35', status: '未启用', createdAt: '2026-06-15', consumption: 51200000 },
  { id: '4', name: '文本生成模型', actualModel: 'DeepSeek-V3', modelType: '文本对话', ipAddress: '192.168.1.13', docUrl: 'https://docs.example.com/deepseek', status: '启用', createdAt: '2026-06-20', consumption: 38700000 },
  { id: '5', name: '图片生成服务', actualModel: 'DALL-E 3', modelType: '图像生成', ipAddress: '192.168.1.14', docUrl: 'https://docs.example.com/dalle3', status: '启用', createdAt: '2026-07-01', consumption: 29500000 },
  { id: '6', name: '语音合成引擎', actualModel: 'TTS-1', modelType: '语音合成', ipAddress: '192.168.2.10', docUrl: 'https://docs.example.com/tts1', status: '未启用', createdAt: '2026-07-05', consumption: 15600000 },
  { id: '7', name: '图像识别服务', actualModel: 'GPT-4V', modelType: '图像识别', ipAddress: '192.168.2.11', docUrl: 'https://docs.example.com/gpt4v', status: '启用', createdAt: '2026-07-08', consumption: 9800000 },
]

const models = ref<ModelItem[]>(mockModels)

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

// ─── CRUD State ───────────────────────────────────────

const showCreateModal = ref(false)
const showEditModal = ref(false)
const showLimitsModal = ref(false)

function emptyModel() {
  return { name: '', actualModel: '', modelType: '', ipAddress: '', docUrl: '', status: '启用', consumption: 0 }
}

const newModel = ref(emptyModel())
const editTarget = ref<{ id: string, createdAt: string } | null>(null)
const editForm = ref(emptyModel())
const limitsTarget = ref<{ userId: string, name: string, modelLimits: ModelLimitEntry[] } | null>(null)
const limitsForm = ref<ModelLimitEntry[]>([])

const brandColor = useCssVar('--color-brand')
const successColor = useCssVar('--color-success')
const accentColor = useCssVar('--color-accent')
const warningColor = useCssVar('--color-warning')
const dangerColor = useCssVar('--color-danger')

const colors = computed(() => [brandColor.value, accentColor.value, successColor.value, warningColor.value, dangerColor.value])

// ─── Charts (API-backed) ─────────────────────────────────

const overviewChartMode = ref<'model' | 'key' | 'total'>('model')
const overviewTimeRange = ref('7d')
const overviewCustomDateRange = ref<[DayjsLike, DayjsLike] | undefined>(undefined)

/** API 返回的时序数据 */
const timeSeriesData = ref<UsageTimeSeries | null>(null)

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

async function fetchTimeSeries() {
  try {
    const params: Record<string, string> = { range: overviewTimeRange.value }
    if (overviewTimeRange.value === 'custom' && overviewCustomDateRange.value) {
      params.startDate = overviewCustomDateRange.value[0]?.format('YYYY-MM-DD')
      params.endDate = overviewCustomDateRange.value[1]?.format('YYYY-MM-DD')
    }
    timeSeriesData.value = await getUsageTimeSeries(overviewTimeRange.value, params)
  } catch {
    message.error('加载趋势数据失败')
  }
}

const overviewTotalCalls = computed(() => {
  const data = timeSeriesData.value
  if (!data) return 0
  if (overviewChartMode.value === 'total') {
    return data.byModel.reduce((sum, m) => sum + m.data.reduce((a, b) => a + b, 0), 0)
  }
  const series = overviewChartMode.value === 'model' ? data.byModel : data.byName
  return series.reduce((sum, s) => sum + s.data.reduce((a, b) => a + b, 0), 0)
})

const overviewTokensPerCall = computed(() => {
  const data = timeSeriesData.value
  if (!data) return 760
  // 统一 name 字段：byModel → modelName, byName → name
  const series = overviewChartMode.value === 'model'
    ? data.byModel.map((m) => ({ name: m.modelName, data: m.data }))
    : data.byName
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
  const data = timeSeriesData.value
  if (!data) return {}
  const categories = data.categories
  const t = chartTheme()

  const base = {
    tooltip: { trigger: 'axis' as const, backgroundColor: t.tooltipBg, borderColor: t.tooltipBorder, borderWidth: 1, textStyle: { color: t.tooltipColor, fontSize: 13 }, valueFormatter: (v: number) => `${v.toLocaleString()} 次` },
    grid: { left: 40, right: 16, bottom: 44, top: 12, containLabel: true },
    xAxis: { type: 'category' as const, data: categories, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11 } },
    yAxis: { type: 'value' as const, min: 0, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: t.axisColor, fontSize: 11 }, splitLine: { lineStyle: { color: t.splitColor, type: 'dashed' as const } } },
    legend: { type: 'scroll' as const, bottom: 0, textStyle: { color: t.legendColor, fontSize: 12 } },
  }

  if (overviewChartMode.value === 'total') {
    // 总调用次数 = 所有 byModel 数据逐日求和
    const totalData = data.byModel[0]?.data.map((_: number, i: number) =>
      data.byModel.reduce((sum: number, m: { data: number[] }) => sum + (m.data[i] || 0), 0),
    ) || []
    return {
      ...base,
      legend: undefined,
      grid: { left: 40, right: 16, bottom: 24, top: 12, containLabel: true },
      series: [{ type: 'bar' as const, data: totalData, barWidth: '32%', itemStyle: { color: makeBarGradient(brandColor.value), borderRadius: [6, 6, 0, 0] }, animationDuration: 600, animationEasing: 'easeOutQuad' as const }],
    }
  }

  // byModel → modelName, byName → name
  const series = overviewChartMode.value === 'model' ? data.byModel.map((m) => ({ name: m.modelName, data: m.data })) : data.byName
  return {
    ...base,
    series: series.map((s, i) => ({
      name: s.name,
      type: 'bar' as const,
      stack: 'total',
      data: s.data,
      barWidth: '44%',
      itemStyle: { color: makeBarGradient(colors.value[i % colors.value.length]), borderRadius: i === series.length - 1 ? [6, 6, 0, 0] : 0 },
      animationDuration: 400 + i * 80,
      animationEasing: 'easeOutQuad' as const,
    })),
  }
})

const userRankingChartOption = computed(() => {
  const t = chartTheme()
  // 升序排列：最大值排在数组末尾 → 横向条形图中显示在最上方
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
      label: {
        show: true,
        position: 'right' as const,
        color: t.axisColor,
        fontSize: 11,
        formatter: (p: { value: number }) => formatConsumption(p.value),
      },
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
      data: [...mockModels].map((m) => ({ name: m.name, value: m.consumption })),
      color: colors.value,
      animationDuration: 600,
    }],
  }
})

// ─── Tab 3: User Rankings & Controls ─────────────────

const allDepartments = ['研发部', '产品部', '运营部', '市场部', '数据部', '技术部', '行政部', '人事部', '财务部', '安全部']
const allModelNames = deployedModelOptions.map((m) => m.value)

const userKeyword = ref('')
const userDepartment = ref<string>('')
const userModel = ref<string[]>([])

const rawUsers: MergedUserRecord[] = (() => {
  const surnames = '张李王赵陈刘杨周吴郑冯蒋沈韩朱'
  const given = ['伟', '芳', '敏', '浩', '洁', '鹏', '丽', '强', '明', '军', '杰', '婷', '勇', '刚', '静', '雷', '娜', '鑫', '丹', '超']
  const depts = allDepartments
  const models = allModelNames
  const users: MergedUserRecord[] = []
  let idx = 0
  for (const s of surnames) {
    for (const g of given) {
      if (idx >= 60) break
      const name = s + g
      const dept = depts[idx % depts.length]
      const calls = Math.round(5000 + Math.random() * 500000)
      const tokens = Math.round(calls * (200 + Math.random() * 800))
      const modelCount = 1 + Math.floor(Math.random() * 4)
      const chosenModels = models.slice(0, modelCount)
      users.push({
        userId: `u-${idx}`,
        name,
        department: dept,
        calls,
        tokens,
        modelLimits: chosenModels.map((mn) => {
          const hasLimit = Math.random() > 0.5
          return {
            modelName: mn,
            enabled: true,
            callsLimit: hasLimit ? Math.round(50000 + Math.random() * 300000) : 0,
            callsWarn: hasLimit ? Math.round(30000 + Math.random() * 200000) : 0,
            tokensLimit: hasLimit ? Math.round(5000000 + Math.random() * 100000000) : 0,
            tokensWarn: hasLimit ? Math.round(3000000 + Math.random() * 80000000) : 0,
          }
        }),
      })
      idx++
    }
  }
  return users
})()

const permissionKeyword = ref('')
const partialOnly = ref(true)
const permissionUsers = computed(() => {
  let list = rawUsers
  const kw = permissionKeyword.value.toLowerCase().trim()
  if (kw) list = list.filter((u) => u.name.includes(kw) || u.department.includes(kw))
  if (partialOnly.value) list = list.filter((u) => (u.modelLimits?.length ?? 0) < allModelNames.length)
  else list = list.filter((u) => (u.modelLimits?.length ?? 0) === allModelNames.length)
  return list
})

// ─── 调用记录 ───────────────────────────────────────────

const callUserKeyword = ref('')
const callModelFilter = ref<string[]>([])
const callStatusFilter = ref<string | undefined>(undefined)
const callRecords = ref<CallRecord[]>([])
const callRecordsLoading = ref(false)

async function fetchCallRecords(): Promise<void> {
  callRecordsLoading.value = true
  try {
    const page = await getUsageRecords({ MaxResultCount: '200' })
    callRecords.value = page.items.map((r) => ({
      id: r.id,
      userName: r.business,
      department: r.usedConfig,
      modelName: r.usedModel,
      inputTokens: r.inputTokens ?? 0,
      outputTokens: r.outputTokens ?? 0,
      userPhone: '',
      latency: r.latencySeconds ? Math.round(r.latencySeconds * 1000) : 0,
      status: r.success ? '成功' : '失败',
      time: r.creationTime.slice(0, 19).replace('T', ' '),
    }))
  } catch {
    message.error('加载调用记录失败')
  } finally {
    callRecordsLoading.value = false
  }
}

const filteredCallRecords = computed(() => {
  let list = callRecords.value
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

// ─── 告警管理 ───────────────────────────────────────────

const alertFilter = ref<'all' | 'calls' | 'tokens'>('all')

const mockAlerts: AlertRecord[] = (() => {
  const now = new Date()
  const alerts: AlertRecord[] = []
  const limitUsers = rawUsers.filter((u) => u.modelLimits.some((m) => m.callsLimit > 0 || m.tokensLimit > 0))
  for (let i = 0; i < limitUsers.length && alerts.length < 30; i++) {
    const u = limitUsers[i]
    for (const m of u.modelLimits) {
      if (!m.callsLimit && !m.tokensLimit) continue
      const d = new Date(now)
      d.setHours(d.getHours() - Math.floor(Math.random() * 72))
      if (m.callsLimit > 0 && Math.random() > 0.4) {
        alerts.push({
          id: `alert-calls-${u.userId}-${m.modelName}`,
          userName: u.name,
          department: u.department,
          modelName: m.modelName,
          type: 'calls',
          current: Math.round(m.callsLimit * (0.85 + Math.random() * 0.2)),
          limit: m.callsLimit,
          time: d.toISOString().slice(0, 19).replace('T', ' '),
        })
      }
      if (m.tokensLimit > 0 && Math.random() > 0.4) {
        const d2 = new Date(now)
        d2.setHours(d2.getHours() - Math.floor(Math.random() * 72))
        alerts.push({
          id: `alert-tokens-${u.userId}-${m.modelName}`,
          userName: u.name,
          department: u.department,
          modelName: m.modelName,
          type: 'tokens',
          current: Math.round(m.tokensLimit * (0.85 + Math.random() * 0.2)),
          limit: m.tokensLimit,
          time: d2.toISOString().slice(0, 19).replace('T', ' '),
        })
      }
    }
  }
  return alerts.sort((a, b) => b.time.localeCompare(a.time))
})()

const alertData = computed(() => {
  if (alertFilter.value === 'all') return mockAlerts
  return mockAlerts.filter((a) => a.type === alertFilter.value)
})

const filteredUsers = computed(() => {
  let list = rawUsers
  if (userKeyword.value) {
    const kw = userKeyword.value.toLowerCase()
    list = list.filter((u) => u.name.includes(kw) || u.department.includes(kw))
  }
  if (userDepartment.value) {
    list = list.filter((u) => u.department === userDepartment.value)
  }
  if (userModel.value.length > 0 && userModel.value.length < allModelNames.length) {
    list = list.filter((u) => u.modelLimits.some((m) => userModel.value.includes(m.modelName)))
  }
  return list.sort((a, b) => b.calls - a.calls)
})

const mergedUserData = computed(() => filteredUsers.value)

// ─── Data Fetching ──────────────────────────────────────

onMounted(() => {
  fetchTimeSeries()
  fetchCallRecords()
})

watch(overviewTimeRange, () => { fetchTimeSeries() })

// ─── CRUD Handlers ────────────────────────────────────

function handleDelete(id: string): void {
  models.value = models.value.filter((m) => m.id !== id)
  message.success('已删除')
}

function handleEdit(record: ModelItem): void {
  editTarget.value = record
  editForm.value = { name: record.name, actualModel: record.actualModel, modelType: record.modelType, ipAddress: record.ipAddress, docUrl: record.docUrl, status: record.status, consumption: record.consumption }
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
      item.name = editForm.value.name
      item.actualModel = editForm.value.actualModel
      item.modelType = editForm.value.modelType
      item.ipAddress = editForm.value.ipAddress
      item.docUrl = editForm.value.docUrl
      item.status = editForm.value.status
    }
  }
  message.success('已更新')
  showEditModal.value = false
  editTarget.value = null
}

function handleCreate(): void {
  if (!newModel.value.name || !newModel.value.actualModel) {
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

const DEFAULT_CALLS_LIMIT = 100000
const DEFAULT_TOKENS_LIMIT = 100000000

function handleEditLimits(user: { userId: string, name: string, modelLimits: ModelLimitEntry[] }): void {
  limitsTarget.value = user
  limitsForm.value = allModelNames.map((mn) => {
    const existing = user.modelLimits.find((m) => m.modelName === mn)
    if (existing) {
      return {
        ...existing,
        callsLimit: existing.callsLimit || DEFAULT_CALLS_LIMIT,
        callsWarn: existing.callsWarn || Math.round((existing.callsLimit || DEFAULT_CALLS_LIMIT) * 0.8),
        tokensLimit: existing.tokensLimit || DEFAULT_TOKENS_LIMIT,
        tokensWarn: existing.tokensWarn || Math.round((existing.tokensLimit || DEFAULT_TOKENS_LIMIT) * 0.8),
      }
    }
    return {
      modelName: mn,
      enabled: true,
      callsLimit: DEFAULT_CALLS_LIMIT,
      callsWarn: Math.round(DEFAULT_CALLS_LIMIT * 0.8),
      tokensLimit: DEFAULT_TOKENS_LIMIT,
      tokensWarn: Math.round(DEFAULT_TOKENS_LIMIT * 0.8),
    }
  })
  showLimitsModal.value = true
}

function handleLimitsOk(): void {
  if (limitsTarget.value) {
    limitsTarget.value.modelLimits.splice(0, limitsTarget.value.modelLimits.length, ...limitsForm.value.filter((m) => m.enabled).map((m) => ({ ...m })))
    message.success('限制已更新')
  }
  showLimitsModal.value = false
  limitsTarget.value = null
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.api-tabs {
  :deep(.ant-tabs-nav) { margin-bottom: @spacing-sm; }
    :deep(.ant-tabs-nav::before) { border-bottom: none; }
  :deep(.ant-tabs-nav-wrap) { padding: 0; }
  :deep(.ant-tabs-nav-list) { gap: 0; }
  :deep(.ant-tabs-tab) { padding: 4px 10px; }
  :deep(.ant-tabs-tab + .ant-tabs-tab) { margin-left: @spacing-md; }
  :deep(.ant-tabs-content-holder) { margin-top: 0; }
}
.api-tabs :deep(.section-card-header) {
  background: transparent;
  border-bottom: none;
  padding: @spacing-sm @spacing-xl;
}
.api-tabs--keys :deep(.section-card-header),
.api-tabs--alerts :deep(.section-card-header) {
  display: none;
}
.api-page--keys :deep(.page-header-right),
.api-page--alerts :deep(.page-header-right) {
  display: none;
}
.api-tabs--calls :deep(.user-filter-bar),
.api-tabs--permissions :deep(.user-filter-bar) {
  display: none;
}
.api-tabs--usage :deep(.dimension-bar),
.api-tabs--usage :deep(.user-filter-bar) {
  display: none;
}
.api-page--permissions :deep(.user-filter-bar) {
  background: @content-bg;
  margin: -1px -1px 0;
  padding: @spacing-sm @spacing-base;
}
.api-page--calls :deep(.user-filter-bar) {
  background: @content-bg;
  margin: -1px -1px 0;
  padding: @spacing-sm @spacing-base;
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
.page-container :deep(.page-header) {
  margin-bottom: @spacing-md;
}

.api-tabs :deep(.ant-tabs-nav .ant-select-selector) {
  height: 24px;
}
.api-tabs :deep(.ant-tabs-nav .ant-select-selection-item),
.api-tabs :deep(.ant-tabs-nav .ant-select-selection-placeholder) {
  line-height: 22px;
}
.api-tabs :deep(.ant-tabs-nav .ant-switch) {
  height: 24px;
  min-width: 80px;
  line-height: 24px;
}
.api-tabs :deep(.ant-tabs-nav .ant-switch-handle) {
  width: 18px;
  height: 18px;
  top: 3px;
}
.api-tabs :deep(.ant-tabs-nav .ant-switch-handle::before) {
  border-radius: 9px;
}
.api-tabs :deep(.ant-tabs-nav .ant-switch-checked .ant-switch-handle) {
  inset-inline-start: calc(100% - 21px);
}
.api-tabs :deep(.ant-tabs-nav .ant-radio-button-wrapper) {
  padding: 0 12px;
}
.api-tabs :deep(.ant-tabs-nav .ant-radio-button-wrapper + .ant-radio-button-wrapper) {
  margin-left: 4px;
}
</style>
