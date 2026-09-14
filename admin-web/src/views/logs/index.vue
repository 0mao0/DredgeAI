<template>
  <div class="page-container">
    <PageHeader title="日志管理" description="系统审计日志查询与详情" />

    <DataTable
      v-model:query="query"
      :columns="columns"
      :data-source="logs"
      row-key="id"
      :loading="loading"
      :pagination="{ current: page, pageSize, total }"
      :filters="filters"
      storage-key="admin-audit-logs"
      @change="handleTableChange"
    >
      <template #toolbarExtra>
        <AppButton size="sm" :loading="refreshing" @click="handleRefresh">
          <ReloadOutlined />
          刷新
        </AppButton>
      </template>
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'executionTime'">
          {{ formatDateTime(record.executionTime, displayTz) }}
        </template>
        <template v-else-if="column.key === 'operator'">
          {{ operatorName(record) }}
        </template>
        <template v-else-if="column.key === 'httpStatusCode'">
          <a-tag :color="statusColor(record.httpStatusCode)">{{ record.httpStatusCode ?? '—' }}</a-tag>
        </template>
        <template v-else-if="column.key === 'duration'">
          {{ record.executionDuration }} ms
        </template>
        <template v-else-if="column.key === 'result'">
          <a-tag :color="record.exceptions ? 'red' : 'green'">{{ record.exceptions ? '异常' : '成功' }}</a-tag>
        </template>
        <template v-else-if="column.key === 'action'">
          <AppButton variant="link" size="sm" @click="openDetail(record)">详情</AppButton>
        </template>
      </template>
    </DataTable>

    <a-drawer :open="detailVisible" title="日志详情" width="640px" @close="detailVisible = false">
      <a-spin :spinning="detailLoading">
        <template v-if="detail">
          <a-descriptions :column="2" size="small" bordered>
            <a-descriptions-item label="时间" :span="2">{{ formatDateTime(detail.executionTime, displayTz) }}</a-descriptions-item>
            <a-descriptions-item label="操作人">{{ operatorName(detail) }}</a-descriptions-item>
            <a-descriptions-item label="角色">{{ detail.operator.roleNames.join('、') || '—' }}</a-descriptions-item>
            <a-descriptions-item label="请求方法">{{ detail.httpMethod ?? '—' }}</a-descriptions-item>
            <a-descriptions-item label="状态码">
              <a-tag :color="statusColor(detail.httpStatusCode)">{{ detail.httpStatusCode ?? '—' }}</a-tag>
            </a-descriptions-item>
            <a-descriptions-item label="URL" :span="2">{{ detail.url ?? '—' }}</a-descriptions-item>
            <a-descriptions-item label="耗时">{{ detail.executionDuration }} ms</a-descriptions-item>
            <a-descriptions-item label="客户端 IP">{{ detail.clientIpAddress ?? '—' }}</a-descriptions-item>
          </a-descriptions>

          <a-alert
            v-if="detail.exceptions"
            class="detail-block"
            type="error"
            show-icon
            message="执行异常"
            :description="detail.exceptions"
          />

          <section class="detail-block">
            <h4 class="detail-block__title">操作记录（{{ detail.actions.length }}）</h4>
            <p v-if="detail.actions.length === 0" class="detail-empty">无</p>
            <ul v-else class="detail-list">
              <li v-for="item in detail.actions" :key="item.id" class="detail-list__item">
                <span class="detail-list__main">{{ item.serviceName }}.{{ item.methodName }}</span>
                <span class="detail-list__meta">{{ item.executionDuration }} ms</span>
              </li>
            </ul>
          </section>

          <section class="detail-block">
            <h4 class="detail-block__title">数据变更（{{ detail.entityChanges.length }}）</h4>
            <p v-if="detail.entityChanges.length === 0" class="detail-empty">无</p>
            <div v-for="change in detail.entityChanges" :key="change.id" class="change-card">
              <div class="change-card__head">
                <a-tag :color="changeColor(change.changeType)">{{ changeLabel(change.changeType) }}</a-tag>
                <span class="change-card__entity">{{ change.entityTypeFullName ?? '—' }}</span>
              </div>
              <ul v-if="change.propertyChanges.length > 0" class="detail-list">
                <li v-for="prop in change.propertyChanges" :key="prop.id" class="detail-list__item">
                  <span class="detail-list__main">{{ prop.propertyName }}</span>
                  <span class="detail-list__meta">{{ shortValue(prop.originalValue) }} → {{ shortValue(prop.newValue) }}</span>
                </li>
              </ul>
            </div>
          </section>
        </template>
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
import { ref, computed, watch, onMounted } from 'vue'
import { message } from 'ant-design-vue'
import { ReloadOutlined } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import { getAuditLogs, getAuditLogDetail } from '@/api/modules/auditLogs'
import type {
  AuditLogDetail,
  AuditLogListItem,
  AuditLogListParams,
  EntityChangeType,
} from '@shared/core/types'
import { formatDateTime, resolveAppConfigTimeZone } from '@shared/web/utils/format'
import { useAppStore } from '@/stores/app'

const appStore = useAppStore()
/** 显示时区：应用配置 timing.timeZone（ABP 时钟为 UTC），缺省 Asia/Shanghai */
const displayTz = computed(() => resolveAppConfigTimeZone(appStore.appConfig))

const page = ref(1)
const pageSize = 15
const total = ref(0)
const loading = ref(false)
const refreshing = ref(false)
const logs = ref<AuditLogListItem[]>([])
const query = ref<Record<string, any>>({})

const filters: DataTableFilter[] = [
  { key: 'url', type: 'input', placeholder: '搜索 URL', width: 240 },
  { key: 'userName', type: 'input', placeholder: '操作人账号', width: 160 },
  { key: 'httpMethod', type: 'select', placeholder: '请求方法', width: 120, options: ['GET', 'POST', 'PUT', 'DELETE'] },
  { key: 'httpStatusCode', type: 'select', placeholder: '状态码', width: 120, options: [200, 400, 401, 403, 404, 500] },
  {
    key: 'hasException',
    type: 'select',
    placeholder: '执行结果',
    width: 120,
    options: [
      { value: 'false', label: '成功' },
      { value: 'true', label: '异常' },
    ],
  },
]

const columns: DataTableColumn[] = [
  { title: '时间', dataIndex: 'executionTime', key: 'executionTime', width: 170, minWidth: 150, resizable: true },
  { title: '操作人', dataIndex: 'operator', key: 'operator', width: 140, minWidth: 120, resizable: true },
  { title: '方法', dataIndex: 'httpMethod', key: 'httpMethod', width: 90, minWidth: 80 },
  { title: 'URL', dataIndex: 'url', key: 'url', minWidth: 240, resizable: true },
  { title: '状态码', dataIndex: 'httpStatusCode', key: 'httpStatusCode', width: 100, minWidth: 90 },
  { title: '耗时', dataIndex: 'executionDuration', key: 'duration', width: 100, minWidth: 90 },
  { title: '客户端 IP', dataIndex: 'clientIpAddress', key: 'clientIpAddress', width: 140, minWidth: 120 },
  { title: '结果', key: 'result', width: 90, minWidth: 80 },
  { title: '操作', key: 'action', width: 100, minWidth: 90, fixed: 'right' },
]

function operatorName(row: AuditLogListItem): string {
  return row.operator?.displayName || row.operator?.userName || '匿名'
}

function statusColor(code: number | null): string {
  if (code === null) return 'default'
  if (code >= 500) return 'red'
  if (code >= 400) return 'orange'
  return 'green'
}

const CHANGE_LABELS: Record<EntityChangeType, string> = { 0: '新增', 1: '修改', 2: '删除' }
const CHANGE_COLORS: Record<EntityChangeType, string> = { 0: 'green', 1: 'blue', 2: 'red' }

function changeLabel(type: EntityChangeType): string {
  return CHANGE_LABELS[type] ?? '变更'
}

function changeColor(type: EntityChangeType): string {
  return CHANGE_COLORS[type] ?? 'default'
}

/** 属性原值/新值可能很长（如 JSON），列表里截断展示 */
function shortValue(value: string | null): string {
  if (!value) return '—'
  return value.length > 80 ? `${value.slice(0, 80)}…` : value
}

function buildParams(): AuditLogListParams {
  const q = query.value
  const params: AuditLogListParams = {
    skipCount: (page.value - 1) * pageSize,
    maxResultCount: pageSize,
    sorting: 'executionTime desc',
  }
  const url = String(q.url ?? '').trim()
  const userName = String(q.userName ?? '').trim()
  if (url) params.url = url
  if (userName) params.userName = userName
  if (q.httpMethod) params.httpMethod = String(q.httpMethod)
  if (q.httpStatusCode !== undefined && q.httpStatusCode !== null && q.httpStatusCode !== '') {
    params.httpStatusCode = Number(q.httpStatusCode)
  }
  if (q.hasException === 'true' || q.hasException === 'false') {
    params.hasException = q.hasException === 'true'
  }
  return params
}

async function fetchLogs(): Promise<void> {
  loading.value = true
  try {
    const res = await getAuditLogs(buildParams())
    logs.value = res.items
    total.value = res.totalCount
  } catch {
    logs.value = []
    total.value = 0
    message.error('加载审计日志失败')
  } finally {
    loading.value = false
  }
}

function handleTableChange(paginationInfo: unknown): void {
  page.value = (paginationInfo as { current?: number } | null)?.current || 1
  fetchLogs()
}

async function handleRefresh(): Promise<void> {
  refreshing.value = true
  try {
    await fetchLogs()
  } finally {
    refreshing.value = false
  }
}

watch(query, () => {
  page.value = 1
  fetchLogs()
})

const detailVisible = ref(false)
const detailLoading = ref(false)
const detail = ref<AuditLogDetail | null>(null)

async function openDetail(record: AuditLogListItem): Promise<void> {
  detailVisible.value = true
  detailLoading.value = true
  detail.value = null
  try {
    detail.value = await getAuditLogDetail(record.id)
  } catch {
    message.error('加载日志详情失败')
    detailVisible.value = false
  } finally {
    detailLoading.value = false
  }
}

onMounted(async () => {
  // 先确保应用配置（含时区）就绪再渲染时间；失败时退回默认时区，不阻塞列表
  await appStore.fetchAppConfig().catch(() => undefined)
  fetchLogs()
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.page-container :deep(.page-header) {
  margin-bottom: @spacing-md;
}

.detail-block {
  margin-top: @spacing-lg;
}

.detail-block__title {
  margin: 0 0 @spacing-sm;
  font-size: @font-size-base;
  font-weight: 500;
  color: @text-primary;
}

.detail-empty {
  margin: 0;
  color: @text-tertiary;
}

.detail-list {
  margin: 0;
  padding: 0;
  list-style: none;
}

.detail-list__item {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: @spacing-sm;
  padding: @spacing-xs 0;
  border-bottom: 1px solid @divider-color;
}

.detail-list__main {
  color: @text-primary;
}

.detail-list__meta {
  color: @text-tertiary;
  font-size: @font-size-sm;
  word-break: break-all;
}

.change-card {
  padding: @spacing-sm;
  margin-bottom: @spacing-sm;
  border: 1px solid @divider-color;
  border-radius: @radius-base;
}

.change-card__head {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
  margin-bottom: @spacing-xs;
}

.change-card__entity {
  color: @text-secondary;
  font-size: @font-size-sm;
  word-break: break-all;
}
</style>
