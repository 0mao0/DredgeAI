<template>
  <div class="page-container">
    <PageHeader title="API 管理" description="管理接入的模型、统计平台用量与配置用户限制" />

    <a-tabs v-model:active-key="activeTab" class="api-tabs">
      <a-tab-pane key="keys" tab="模型管理">
        <SectionCard nopad>
          <template #extra>
            <a-button type="primary" size="small" @click="showCreateModal = true">
              <PlusOutlined />
              添加模型
            </a-button>
          </template>
          <a-table
            size="small"
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
              <template v-else-if="column.key === 'consumption'">
                {{ formatConsumption(record.consumption) }}
              </template>
              <template v-else-if="column.key === 'doc'">
                <a-button type="link" size="small" @click="openDoc(record.docUrl)">
                  <FileTextOutlined /> 文档
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

      <a-tab-pane key="calls" tab="调用记录">
        <SectionCard nopad>
          <div class="user-filter-bar">
            <a-input-search
              v-model:value="callUserKeyword"
              placeholder="搜索用户"
              allow-clear
              style="width:180px"
            />
            <a-select
              v-model:value="callModelFilter"
              mode="multiple"
              allow-clear
              placeholder="模型"
              :max-tag-count="0"
              :max-tag-placeholder="callModelFilter.length ? `已选 ${callModelFilter.length}` : '全部'"
              style="width:140px"
            >
              <a-select-option v-for="m in allModelNames" :key="m" :value="m">{{ m }}</a-select-option>
            </a-select>
            <a-select v-model:value="callStatusFilter" allow-clear placeholder="状态" style="width:100px">
              <a-select-option value="成功">成功</a-select-option>
              <a-select-option value="失败">失败</a-select-option>
            </a-select>
          </div>
          <a-table
            size="small"
            :data-source="callRecords"
            :columns="callColumns"
            :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 条` }"
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
          </a-table>
        </SectionCard>
      </a-tab-pane>

      <a-tab-pane key="usage" tab="用量分析">
        <div class="stats-tab">
          <div class="dimension-bar">
            <a-segmented v-model:value="usageDimension" :options="['模型维度', '用户维度']" />
          </div>

          <template v-if="usageDimension === '模型维度'">
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
              <SectionCard title="模型消耗排名">
                <ChartContainer :option="userRankingChartOption" height="340px" />
              </SectionCard>
            </a-col>
            <a-col :span="10" class="mb-24">
              <SectionCard title="模型用量占比">
                <ChartContainer :option="modelPieOption" height="340px" />
              </SectionCard>
            </a-col>
          </a-row>
          </template>

          <template v-if="usageDimension === '用户维度'">
          <SectionCard nopad>
            <div class="user-filter-bar">
              <a-input-search
                v-model:value="userKeyword"
                placeholder="搜索姓名 / 部门"
                allow-clear
                style="width:200px"
              />
              <a-select v-model:value="userDepartment" allow-clear placeholder="部门" style="width:140px">
                <a-select-option v-for="d in allDepartments" :key="d" :value="d">{{ d }}</a-select-option>
              </a-select>
              <a-select
                v-model:value="userModel"
                mode="multiple"
                allow-clear
                placeholder="全部"
                :max-tag-count="0"
                :max-tag-placeholder="userModel.length === 0 || userModel.length === allModelNames.length ? '全部' : `已选 ${userModel.length} 项`"
                style="width:140px"
              >
                <a-select-option v-for="m in allModelNames" :key="m" :value="m">{{ m }}</a-select-option>
              </a-select>
            </div>
            <a-table
              size="small"
              :data-source="mergedUserData"
              :columns="consumptionColumns"
              :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 人` }"
              row-key="userId"
            >
              <template #bodyCell="{ column, record, index }">
                <template v-if="column.key === 'rank'">
                  <span class="rank-badge" :class="[{ gold: index < 3 }]">{{ index + 1 }}</span>
                </template>
                <template v-else-if="column.key === 'calls'">
                  {{ formatNumber(record.calls) }}
                </template>
                <template v-else-if="column.key === 'tokens'">
                  {{ formatNumber(record.tokens) }}
                </template>
                <template v-else-if="column.key === 'models'">
                  <a-tag :color="(record.modelLimits?.length ?? 0) === allModelNames.length ? 'green' : 'orange'">{{ (record.modelLimits?.length ?? 0) === allModelNames.length ? '全部' : '部分' }}</a-tag>
                </template>
              </template>
            </a-table>
          </SectionCard>
          </template>
        </div>
      </a-tab-pane>

      <a-tab-pane key="permissions" tab="权限控制">
        <SectionCard nopad>
          <div class="user-filter-bar">
            <a-input-search
              v-model:value="permissionKeyword"
              placeholder="搜索姓名 / 部门"
              allow-clear
              style="width:200px"
            />
            <a-switch
              v-model:checked="partialOnly"
              checked-children="部分权限"
              un-checked-children="全部权限"
              class="perm-switch"
            />
          </div>
          <a-table
            size="small"
            :data-source="permissionUsers"
            :columns="permissionColumns"
            :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 人` }"
            row-key="userId"
          >
            <template #bodyCell="{ column, record, index }">
              <template v-if="column.key === 'index'">
                {{ index + 1 }}
              </template>
              <template v-else-if="column.key === 'models'">
                <a-tag :color="(record.modelLimits?.length ?? 0) === allModelNames.length ? 'green' : 'orange'">{{ (record.modelLimits?.length ?? 0) === allModelNames.length ? '全部' : '部分' }}</a-tag>
              </template>
              <template v-else-if="column.key === 'callsLimit'">
                {{ formatLimit(record, 'calls') }}
              </template>
              <template v-else-if="column.key === 'tokensLimit'">
                {{ formatLimit(record, 'tokens') }}
              </template>
              <template v-else-if="column.key === 'action'">
                <a-button type="link" size="small" @click="handleEditLimits(record)">编辑</a-button>
              </template>
            </template>
          </a-table>
        </SectionCard>
      </a-tab-pane>

      <a-tab-pane key="alerts" tab="告警管理">
        <SectionCard nopad>
          <template #extra>
            <a-radio-group v-model:value="alertFilter" size="small" button-style="solid">
              <a-radio-button value="all">全部</a-radio-button>
              <a-radio-button value="calls">调用超限</a-radio-button>
              <a-radio-button value="tokens">Token 超限</a-radio-button>
            </a-radio-group>
          </template>
          <a-table
            size="small"
            :data-source="alertData"
            :columns="alertColumns"
            :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 条` }"
            row-key="id"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'type'">
                <a-tag :color="record.type === 'calls' ? 'orange' : 'purple'">{{ record.type === 'calls' ? '调用超限' : 'Token 超限' }}</a-tag>
              </template>
              <template v-else-if="column.key === 'ratio'">
                {{ formatNumber(record.current) }} / {{ formatNumber(record.limit) }}
              </template>
            </template>
          </a-table>
        </SectionCard>
      </a-tab-pane>
    </a-tabs>

    <!-- Create Modal -->
    <a-modal v-model:open="showCreateModal" title="添加模型" @ok="handleCreate" @cancel="resetNewModel()">
      <a-form layout="horizontal" :label-col="{ span: 7 }" :wrapper-col="{ span: 17 }">
        <a-form-item label="模型名称" required>
          <a-input v-model:value="newModel.name" placeholder="用户自定义名称" />
        </a-form-item>
        <a-form-item label="实际模型" required>
          <a-select v-model:value="newModel.actualModel" :options="deployedModelOptions" placeholder="选择已部署的模型" />
        </a-form-item>
        <a-form-item label="模型类型" required>
          <a-select v-model:value="newModel.modelType" :options="modelTypeOptions" placeholder="选择模型分类" />
        </a-form-item>
        <a-form-item label="IP 地址">
          <a-input v-model:value="newModel.ipAddress" placeholder="如：192.168.1.100" />
        </a-form-item>
        <a-form-item label="API 文档链接">
          <a-input v-model:value="newModel.docUrl" placeholder="https://docs.example.com/model" />
        </a-form-item>
        <a-form-item label="状态">
          <a-select v-model:value="newModel.status" :options="statusOptions" />
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- Edit Modal -->
    <a-modal v-model:open="showEditModal" title="编辑模型" @ok="handleEditOk">
      <a-form v-if="editTarget" layout="horizontal" :label-col="{ span: 7 }" :wrapper-col="{ span: 17 }">
        <a-form-item label="模型名称" required>
          <a-input v-model:value="editForm.name" placeholder="用户自定义名称" />
        </a-form-item>
        <a-form-item label="实际模型" required>
          <a-select v-model:value="editForm.actualModel" :options="deployedModelOptions" placeholder="选择已部署的模型" />
        </a-form-item>
        <a-form-item label="模型类型" required>
          <a-select v-model:value="editForm.modelType" :options="modelTypeOptions" placeholder="选择模型分类" />
        </a-form-item>
        <a-form-item label="IP 地址">
          <a-input v-model:value="editForm.ipAddress" placeholder="如：192.168.1.100" />
        </a-form-item>
        <a-form-item label="API 文档链接">
          <a-input v-model:value="editForm.docUrl" placeholder="https://docs.example.com/model" />
        </a-form-item>
        <a-form-item label="状态">
          <a-select v-model:value="editForm.status" :options="statusOptions" />
        </a-form-item>
        <a-form-item label="创建日期">
          <span class="readonly-field">{{ editTarget.createdAt }}</span>
        </a-form-item>
      </a-form>
    </a-modal>

    <!-- Edit Limits Modal -->
    <a-modal v-model:open="showLimitsModal" title="编辑用户限制" width="820px" @ok="handleLimitsOk">
      <template v-if="limitsTarget">
        <p class="limits-dimension">限制维度：每周</p>
        <a-table
          :data-source="limitsForm"
          :columns="limitColumns"
          row-key="modelName"
          :pagination="false"
          size="small"
          class="limits-table"
        >
          <template #bodyCell="{ column, record, index }">
            <template v-if="column.key === 'enabled'">
              <a-checkbox v-model:checked="limitsForm[index].enabled" />
            </template>
            <template v-else-if="column.key === 'modelName'">
              <span :class="{ 'model-name--disabled': !record.enabled }">{{ record.modelName }}</span>
            </template>
            <template v-else-if="column.key === 'callsLimit'">
              <a-input-number v-model:value="limitsForm[index].callsLimit" :min="0" :disabled="!record.enabled" style="width:100%" placeholder="0=无限制" />
            </template>
            <template v-else-if="column.key === 'callsWarn'">
              <a-input-number v-model:value="limitsForm[index].callsWarn" :min="0" :disabled="!record.enabled" style="width:100%" placeholder="0=不预警" />
            </template>
            <template v-else-if="column.key === 'tokensLimit'">
              <a-input-number v-model:value="limitsForm[index].tokensLimit" :min="0" :disabled="!record.enabled" style="width:100%" placeholder="0=无限制" />
            </template>
            <template v-else-if="column.key === 'tokensWarn'">
              <a-input-number v-model:value="limitsForm[index].tokensWarn" :min="0" :disabled="!record.enabled" style="width:100%" placeholder="0=不预警" />
            </template>
          </template>
        </a-table>
      </template>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'
import { message } from 'ant-design-vue'
import { PlusOutlined, FileTextOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import ChartContainer from '@shared/web/components/ChartContainer.vue'
import MetricCard from '@shared/web/components/MetricCard.vue'
import { formatNumber } from '@shared/core/utils/format'
import { useCssVar } from '@shared/web/composables/useCssVar'
import { useChartTheme } from '@shared/web/composables/useChartTheme'
import { getUsageTimeSeries } from '@/api/modules/apikey'
import type { UsageTimeSeries } from '@/types'

const { chartTheme } = useChartTheme()

const activeTab = ref('keys')
const usageDimension = ref('模型维度')

// ─── Models (CRUD local state, no backend API) ──────────

interface ModelItem {
  id: string
  name: string
  actualModel: string
  modelType: string
  ipAddress: string
  docUrl: string
  status: string
  createdAt: string
  consumption: number
}

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


// ─── Columns ──────────────────────────────────────────

function formatConsumption(n: number): string {
  if (n >= 1e12) return (n / 1e12).toFixed(1) + ' 兆'
  if (n >= 1e8) return (n / 1e8).toFixed(1) + ' 亿'
  if (n >= 1e7) return (n / 1e7).toFixed(1) + ' 千万'
  if (n >= 1e4) return (n / 1e4).toFixed(1) + ' 万'
  return n.toLocaleString()
}

function formatLimit(record: MergedUserRecord, field: 'calls' | 'tokens'): string {
  const total = record.modelLimits
    .filter((m) => m.enabled)
    .reduce((s, m) => s + (field === 'calls' ? m.callsLimit : m.tokensLimit), 0)
  if (total === 0) return '-'
  return formatConsumption(total).replace(/\s/g, '') + '/周'
}

const modelColumns = [
  { title: '序号', key: 'index', width: 70, align: 'center' },
  { title: '模型名称', dataIndex: 'name', key: 'name', align: 'center' },
  { title: '模型类型', dataIndex: 'modelType', key: 'modelType', align: 'center' },
  { title: '消耗额度', key: 'consumption', align: 'center' },
  { title: '状态', key: 'status', width: 100, align: 'center' },
  { title: 'API 文档', key: 'doc', width: 110, align: 'center' },
  { title: '操作', key: 'action', width: 130, align: 'center' },
]

const limitColumns = [
  { title: '启用', key: 'enabled', width: 60, align: 'center' },
  { title: '模型', key: 'modelName', width: 130 },
  { title: '调用次限制', key: 'callsLimit', width: 150 },
  { title: '调用次预警', key: 'callsWarn', width: 150 },
  { title: 'Token 限制', key: 'tokensLimit', width: 150 },
  { title: 'Token 预警', key: 'tokensWarn', width: 150 },
]

interface ModelLimitEntry {
  modelName: string
  enabled: boolean
  callsLimit: number
  callsWarn: number
  tokensLimit: number
  tokensWarn: number
}

interface MergedUserRecord {
  userId: string
  name: string
  department: string
  calls: number
  tokens: number
  modelLimits: ModelLimitEntry[]
}

const consumptionColumns = [
  { title: '排名', key: 'rank', width: 70, align: 'center' },
  { title: '用户', dataIndex: 'name', key: 'name', align: 'center' },
  { title: '部门', dataIndex: 'department', key: 'department', align: 'center' },
  { title: '总调用次数', key: 'calls', align: 'center', sorter: (a: MergedUserRecord, b: MergedUserRecord) => a.calls - b.calls, sortDirections: ['ascend', 'descend'] as const },
  { title: '总 Token 用量', key: 'tokens', align: 'center', sorter: (a: MergedUserRecord, b: MergedUserRecord) => a.tokens - b.tokens, sortDirections: ['ascend', 'descend'] as const },
  { title: '授权模型', key: 'models', width: 100, align: 'center' },
]

const permissionColumns = [
  { title: '序号', key: 'index', width: 70, align: 'center' },
  { title: '用户', dataIndex: 'name', key: 'name', width: 100, align: 'center' },
  { title: '部门', dataIndex: 'department', key: 'department', width: 100, align: 'center' },
  { title: '已授权模型', key: 'models', width: 120, align: 'center' },
  { title: '调用限制', key: 'callsLimit', width: 120, align: 'center' },
  { title: 'Token 限制', key: 'tokensLimit', width: 120, align: 'center' },
  { title: '操作', key: 'action', width: 100, align: 'center' },
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
const overviewCustomDateRange = ref<any>(undefined)

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

// ─── 调用记录 ───────────────────────────────────────────

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

const phones = ['13800138001', '13900139002', '13700137003', '13600136004', '13500135005', '15800158006', '15900159007', '15700157008', '15600156009', '15500155010', '15200152011', '15300153012', '15100151013', '15000150014', '18800188015', '18900189016', '18600186017', '18700187018', '18500185019', '18400184020']

const mockCallRecords: CallRecord[] = (() => {
  const now = new Date()
  const records: CallRecord[] = []
  const userNames = rawUsers.map((u) => u.name)
  for (let i = 0; i < 200; i++) {
    const d = new Date(now)
    d.setMinutes(d.getMinutes() - Math.floor(Math.random() * 4320))
    const userIdx = Math.floor(Math.random() * userNames.length)
    const modelIdx = Math.floor(Math.random() * mockModels.length)
    const success = Math.random() > 0.15
    records.push({
      id: `call-${i}`,
      userName: userNames[userIdx],
      department: rawUsers[userIdx].department,
    modelName: mockModels[modelIdx].actualModel,
    inputTokens: Math.round(100 + Math.random() * 4000),
    outputTokens: Math.round(100 + Math.random() * 4000),
      userPhone: phones[userIdx % phones.length],
      latency: success ? Math.round(300 + Math.random() * 5000) : Math.round(8000 + Math.random() * 12000),
      status: success ? '成功' : '失败',
      time: d.toISOString().slice(0, 19).replace('T', ' '),
    })
  }
  return records.sort((a, b) => b.time.localeCompare(a.time))
})()

const callColumns = [
  { title: '时间', dataIndex: 'time', key: 'time', width: 160, align: 'center' },
  { title: '模型', dataIndex: 'modelName', key: 'modelName', align: 'center' },
  { title: '用户', key: 'userName', width: 100, align: 'center' },
  { title: '输入 Token', key: 'inputTokens', width: 100, align: 'center' },
  { title: '输出 Token', key: 'outputTokens', width: 100, align: 'center' },
  { title: '延迟', key: 'latency', width: 90, align: 'center' },
  { title: '状态', key: 'status', width: 80, align: 'center' },
]

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

// ─── 告警管理 ───────────────────────────────────────────

interface AlertRecord {
  id: string
  userName: string
  department: string
  modelName: string
  type: 'calls' | 'tokens'
  current: number
  limit: number
  time: string
}

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

const alertColumns = [
  { title: '用户', dataIndex: 'userName', key: 'userName', align: 'center' },
  { title: '部门', dataIndex: 'department', key: 'department', align: 'center' },
  { title: '模型', dataIndex: 'modelName', key: 'modelName', align: 'center' },
  { title: '超限类型', key: 'type', width: 110, align: 'center' },
  { title: '当前用量 / 限制', key: 'ratio', align: 'center' },
  { title: '时间', dataIndex: 'time', key: 'time', width: 160, align: 'center' },
]

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
})

watch(overviewTimeRange, () => { fetchTimeSeries() })

// ─── CRUD Handlers ────────────────────────────────────

function openDoc(url: string): void {
  window.open(url, '_blank')
}

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
.page-container :deep(.page-header) {
  margin-bottom: @spacing-md;
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

.limits-table {
  margin-top: @spacing-md;
}
.model-name--disabled {
  color: @text-tertiary;
}
.limits-dimension {
  font-weight: @font-weight-semibold;
  margin-bottom: @spacing-md;
  color: @text-primary;
}

.readonly-field {
  color: @text-secondary;
  padding: 4px 0;
  display: inline-block;
  line-height: 32px;
}

.dimension-bar {
  display: flex;
  justify-content: flex-end;
  margin-bottom: @spacing-md;
}

.stats-tab :deep(.section-card-header) {
  padding: @spacing-md @spacing-xl;
}
.stats-tab :deep(.section-card-body) {
  padding: @spacing-md @spacing-xl;
}

.user-filter-bar {
  display: flex;
  gap: @spacing-sm;
  align-items: center;
  padding: @spacing-md @spacing-xl;
  border-bottom: 1px solid @border-color;
  flex-wrap: wrap;
}
.perm-switch {
  margin-left: auto;
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
    background: @brand-gradient;
    color: #fff;
  }
}
</style>
