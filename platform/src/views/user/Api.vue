<template>
  <div class="page-container">
    <div class="page-header">
      <h2>API 管理</h2>
      <p>管理 API Key 与查看调用用量</p>
    </div>

    <a-card class="key-list-section" :bordered="false">
      <div class="key-list-header">
        <h3>API Key 列表</h3>
        <a-button type="primary" @click="showCreateModal = true">
          <PlusOutlined /> 新建 Key
        </a-button>
      </div>
      <a-table
        :data-source="apiKeys"
        :columns="keyColumns"
        :pagination="{ pageSize: 10 }"
        size="middle"
        row-key="id"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'key'">
            <a-typography-paragraph :copyable="{ text: record.fullKey }" style="margin: 0">
              <code>{{ record.key }}</code>
            </a-typography-paragraph>
          </template>
          <template v-if="column.key === 'status'">
            <a-tag :color="record.status === '启用' ? 'green' : 'default'">{{ record.status }}</a-tag>
          </template>
          <template v-if="column.key === 'usage'">
            <span v-if="record.usage > 0">{{ formatNum(record.usage) }}</span>
            <span v-else class="no-usage">-</span>
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" size="small" @click="onEdit(record)">编辑</a-button>
            <a-button type="link" size="small" :danger="record.status === '启用'" @click="onToggleStatus(record)">
              {{ record.status === '启用' ? '禁用' : '启用' }}
            </a-button>
            <a-button type="link" size="small" danger @click="onDelete(record)">删除</a-button>
            <a-button type="link" size="small" @click="onOpenDoc(record)">
              <FileTextOutlined /> API 文档
            </a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-row :gutter="16" class="stats-row">
      <a-col :span="6">
        <a-card class="stat-card" :bordered="false">
          <div class="stat-label">总 Key 数</div>
          <div class="stat-value">{{ apiKeys.length }}</div>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card class="stat-card" :bordered="false">
          <div class="stat-label">本月调用量</div>
          <div class="stat-value">24,580</div>
          <div class="stat-trend up">+12.5%</div>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card class="stat-card" :bordered="false">
          <div class="stat-label">本月费用</div>
          <div class="stat-value">¥1,234.50</div>
          <div class="stat-trend up">+8.3%</div>
        </a-card>
      </a-col>
      <a-col :span="6">
        <a-card class="stat-card" :bordered="false">
          <div class="stat-label">活跃 Key</div>
          <div class="stat-value">{{ activeKeyCount }}</div>
        </a-card>
      </a-col>
    </a-row>

    <a-card class="usage-section" :bordered="false">
      <div class="usage-header">
        <h3>用量概览</h3>
        <div class="time-filter">
          <a-segmented :value="timeRange" :options="timeRangeOptions" @change="onTimeRangeChange" />
          <a-date-picker
            v-if="timeRange === 'custom'"
            v-model:value="customRange"
            :picker="'month'"
            style="width: 140px; margin-left: 8px"
            placeholder="选择月份"
          />
        </div>
      </div>
      <a-row :gutter="24">
        <a-col :span="12">
          <div class="usage-title">按模型</div>
          <div v-for="item in usageByModel" :key="item.modelName" class="usage-bar-row">
            <div class="usage-bar-label">
              <span>{{ item.modelName }}</span>
              <span class="usage-bar-num">{{ formatNum(item.calls) }}</span>
            </div>
            <a-progress
              :percent="item.share"
              :show-info="false"
              :stroke-color="getModelColor(item.modelName)"
              size="small"
            />
          </div>
        </a-col>
        <a-col :span="12">
          <div class="usage-title">按 Key</div>
          <div v-for="item in usageByKey" :key="item.keyName" class="usage-bar-row">
            <div class="usage-bar-label">
              <span>{{ item.keyName }}</span>
              <span class="usage-bar-num">{{ formatNum(item.calls) }}</span>
            </div>
            <a-progress
              :percent="item.share"
              :show-info="false"
              size="small"
            />
          </div>
        </a-col>
      </a-row>
    </a-card>

    <a-modal
      v-model:visible="showCreateModal"
      title="新建 API Key"
      :footer="null"
      width="520"
      :destroyOnClose="true"
      @cancel="onCancelCreate"
    >
      <a-form :model="createForm" layout="vertical">
        <a-form-item label="名称" required>
          <a-input v-model:value="createForm.name" placeholder="例如：生产环境、测试环境" />
        </a-form-item>
        <a-form-item label="模型类型" required>
          <a-select v-model:value="createForm.modelType" placeholder="选择模型类型">
            <a-select-option v-for="m in modelTypes" :key="m.id" :value="m.name">{{ m.name }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="自动生成 Key">
          <a-input v-model:value="generatedKey" disabled>
            <template #suffix>
              <a-button type="link" size="small" @click="regenerateKey">重新生成</a-button>
            </template>
          </a-input>
        </a-form-item>
        <a-form-item>
          <a-button type="primary" block :disabled="!canCreate" @click="onCreate">创建 Key</a-button>
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:visible="showEditModal"
      title="编辑 API Key"
      :footer="null"
      width="520"
      :destroyOnClose="true"
    >
      <a-form :model="editForm" layout="vertical">
        <a-form-item label="名称" required>
          <a-input v-model:value="editForm.name" />
        </a-form-item>
        <a-form-item label="模型类型" required>
          <a-select v-model:value="editForm.modelType" placeholder="选择模型类型">
            <a-select-option v-for="m in modelTypes" :key="m.id" :value="m.name">{{ m.name }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item>
          <a-button type="primary" block @click="onSaveEdit">保存</a-button>
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { PlusOutlined, FileTextOutlined } from '@ant-design/icons-vue'
import { apiKeys as mockApiKeys, modelTypes as mockModelTypes, usageByModel as mockUsageByModel, usageByKey as mockUsageByKey } from '@/mock/data'

const timeRange = ref<string>('thisMonth')
const timeRangeOptions = [
  { label: '本月', value: 'thisMonth' },
  { label: '上月', value: 'lastMonth' },
  { label: '近7天', value: 'last7Days' },
  { label: '近30天', value: 'last30Days' },
  { label: '自定义', value: 'custom' },
]
const customRange = ref(null)

const apiKeys = ref([...mockApiKeys])
const usageByModel = ref([...mockUsageByModel])
const usageByKey = ref([...mockUsageByKey])
const modelTypes = ref([...mockModelTypes])

const activeKeyCount = computed(() => apiKeys.value.filter(k => k.status === '启用').length)

const keyColumns = [
  { title: '名称', dataIndex: 'name', key: 'name' },
  { title: '模型类型', dataIndex: 'modelType', key: 'modelType' },
  { title: 'API Key', dataIndex: 'key', key: 'key' },
  { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt' },
  { title: '状态', dataIndex: 'status', key: 'status' },
  { title: '本月用量', dataIndex: 'usage', key: 'usage' },
  { title: '操作', key: 'action', width: 280 },
]

const showCreateModal = ref(false)
const showEditModal = ref(false)

const createForm = ref({ name: '', modelType: '' })
const editForm = ref({ id: '', name: '', modelType: '' })

const generatedKey = ref(generateMockKey())

function generateMockKey(): string {
  const chars = 'abcdefghijklmnopqrstuvwxyz0123456789'
  let r = ''
  for (let i = 0; i < 24; i++) r += chars[Math.floor(Math.random() * chars.length)]
  return 'sk-dg-' + r
}

function regenerateKey() {
  generatedKey.value = generateMockKey()
}

const canCreate = computed(() => createForm.value.name.trim() && createForm.value.modelType)

const modelColors: Record<string, string> = {
  'GPT-4o': '#00c9b7',
  'Claude 3.5 Sonnet': '#1a2332',
  'DeepSeek-V3': '#6366f1',
  '通义千问-Max': '#f59e0b',
  '本地模型': '#94a3b8',
}

function getModelColor(name: string): string {
  return modelColors[name] || '#00c9b7'
}

function formatNum(n: number): string {
  if (n >= 10000) return (n / 10000).toFixed(1) + '万'
  if (n >= 1000) return (n / 1000).toFixed(1) + 'k'
  return String(n)
}

function onTimeRangeChange(v: string | number) {
  timeRange.value = v as string
}

function onOpenDoc(record: any) {
  window.open(record.docUrl, '_blank')
}

function onEdit(record: any) {
  editForm.value = { id: record.id, name: record.name, modelType: record.modelType }
  showEditModal.value = true
}

function onSaveEdit() {
  const idx = apiKeys.value.findIndex(k => k.id === editForm.value.id)
  if (idx >= 0) {
    apiKeys.value[idx] = { ...apiKeys.value[idx], name: editForm.value.name, modelType: editForm.value.modelType }
  }
  showEditModal.value = false
}

function onToggleStatus(record: any) {
  const idx = apiKeys.value.findIndex(k => k.id === record.id)
  if (idx >= 0) {
    apiKeys.value[idx] = {
      ...apiKeys.value[idx],
      status: apiKeys.value[idx].status === '启用' ? '禁用' : '启用',
    }
  }
}

function onDelete(record: any) {
  apiKeys.value = apiKeys.value.filter(k => k.id !== record.id)
}

function onCreate() {
  const newKey: any = {
    id: String(Date.now()),
    name: createForm.value.name,
    key: generatedKey.value.slice(0, 12) + '****' + generatedKey.value.slice(-4),
    fullKey: generatedKey.value,
    modelType: createForm.value.modelType,
    createdAt: new Date().toISOString().slice(0, 10),
    status: '启用',
    usage: 0,
    docUrl: 'https://docs.dredgeai.com/api/' + createForm.value.modelType.toLowerCase().replace(/[^a-z0-9]/g, ''),
  }
  apiKeys.value.unshift(newKey)
  showCreateModal.value = false
}

function onCancelCreate() {
  createForm.value = { name: '', modelType: '' }
  regenerateKey()
}
</script>

<style scoped lang="less">
@import '@/styles/variables.less';

.stats-row {
  margin-bottom: 16px;
}
.stat-card {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
}
.stat-label {
  font-size: 13px;
  color: @text-secondary;
  margin-bottom: 4px;
}
.stat-value {
  font-size: 24px;
  font-weight: 700;
  color: @text-primary;
}
.stat-trend {
  font-size: 12px;
  margin-top: 4px;
  &.up { color: #52c41a; }
  &.down { color: #ff4d4f; }
}

.usage-section {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
  margin-bottom: 16px;
}
.usage-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
  h3 { margin: 0; }
}
.time-filter {
  display: flex;
  align-items: center;
}

.usage-title {
  font-size: 14px;
  font-weight: 600;
  color: @text-primary;
  margin-bottom: 12px;
}
.usage-bar-row {
  margin-bottom: 12px;
}
.usage-bar-label {
  display: flex;
  justify-content: space-between;
  font-size: 13px;
  color: @text-primary;
  margin-bottom: 4px;
}
.usage-bar-num {
  color: @text-secondary;
  font-size: 12px;
}

.key-list-section {
  border-radius: @border-radius;
  box-shadow: @shadow-sm;
  margin-bottom: 16px;
}
.key-list-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
  h3 { margin: 0; }
}

.no-usage {
  color: @text-secondary;
}
</style>
