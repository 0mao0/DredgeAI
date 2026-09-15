<template>
  <div class="page-container">
    <PageHeader title="日志管理" description="查询系统操作与请求审计日志" />

    <DataTable
      v-model:query="query"
      :columns="columns"
      :data-source="logs"
      row-key="id"
      :loading="loading"
      :pagination="{ current: page, pageSize, total }"
      :filters="filters"
      storage-key="audit-logs"
      :scroll="{ x: 1100 }"
      @change="handleTableChange"
    >
      <template #toolbarExtra>
        <a-range-picker
          v-model:value="timeRange"
          show-time
          size="small"
          :placeholder="['开始时间', '结束时间']"
        />
        <AppButton size="sm" :loading="refreshing" @click="handleRefresh">
          <ReloadOutlined />
          刷新
        </AppButton>
      </template>
      <template #bodyCell="{ column, record, index }">
        <template v-if="column.key === 'index'">
          {{ (page - 1) * pageSize + index + 1 }}
        </template>
        <template v-else-if="column.key === 'httpMethod'">
          <a-tag v-if="record.httpMethod" :color="methodColor(record.httpMethod)">{{ record.httpMethod }}</a-tag>
          <span v-else>—</span>
        </template>
        <template v-else-if="column.key === 'userName'">
          {{ record.operator?.userName ?? record.operator?.displayName ?? '—' }}
        </template>
        <template v-else-if="column.key === 'url'">
          {{ record.url ?? '—' }}
        </template>
        <template v-else-if="column.key === 'httpStatusCode'">
          <a-tag v-if="record.httpStatusCode != null" :color="statusColor(record.httpStatusCode)">
            {{ record.httpStatusCode }}
          </a-tag>
          <span v-else>—</span>
        </template>
        <template v-else-if="column.key === 'executionDuration'">
          {{ record.executionDuration }} ms
        </template>
        <template v-else-if="column.key === 'action'">
          <AppButton variant="link" size="sm" @click="openDetail(record.id)">详情</AppButton>
        </template>
      </template>
    </DataTable>

    <a-drawer
      :open="detailVisible"
      title="日志详情"
      width="640px"
      @close="detailVisible = false"
    >
      <a-spin :spinning="detailLoading">
        <template v-if="detail">
          <a-descriptions size="small" :column="2" bordered>
            <a-descriptions-item label="操作人">
              {{ detail.operator.userName ?? '—' }}
              <template v-if="detail.operator.displayName">（{{ detail.operator.displayName }}）</template>
            </a-descriptions-item>
            <a-descriptions-item label="应用">{{ detail.applicationName ?? '—' }}</a-descriptions-item>
            <a-descriptions-item label="时间">{{ detail.executionTime }}</a-descriptions-item>
            <a-descriptions-item label="耗时">{{ detail.executionDuration }} ms</a-descriptions-item>
            <a-descriptions-item label="HTTP 方法">
              <a-tag v-if="detail.httpMethod" :color="methodColor(detail.httpMethod)">{{ detail.httpMethod }}</a-tag>
              <span v-else>—</span>
            </a-descriptions-item>
            <a-descriptions-item label="状态码">
              <a-tag v-if="detail.httpStatusCode != null" :color="statusColor(detail.httpStatusCode)">
                {{ detail.httpStatusCode }}
              </a-tag>
              <span v-else>—</span>
            </a-descriptions-item>
            <a-descriptions-item label="URL" :span="2">{{ detail.url ?? '—' }}</a-descriptions-item>
            <a-descriptions-item label="客户端 IP">{{ detail.clientIpAddress ?? '—' }}</a-descriptions-item>
            <a-descriptions-item label="关联 ID">{{ detail.correlationId ?? '—' }}</a-descriptions-item>
            <a-descriptions-item label="浏览器" :span="2">{{ detail.browserInfo ?? '—' }}</a-descriptions-item>
            <a-descriptions-item v-if="detail.exceptions" label="异常" :span="2">
              <pre class="exception-pre">{{ detail.exceptions }}</pre>
            </a-descriptions-item>
            <a-descriptions-item label="备注" :span="2">{{ detail.comments ?? '—' }}</a-descriptions-item>
          </a-descriptions>

          <a-tabs class="drawer-tabs">
            <a-tab-pane key="actions" :tab="`操作（${detail.actions.length}）`">
              <a-table
                :columns="actionColumns"
                :data-source="detail.actions"
                row-key="id"
                size="small"
                :pagination="false"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.key === 'serviceName' || column.key === 'methodName'">
                    <span class="cell-wrap">{{ record[column.key] ?? '—' }}</span>
                  </template>
                  <template v-else-if="column.key === 'parameters'">
                    <span class="params-cell">{{ decodeParams(record.parameters) }}</span>
                  </template>
                  <template v-else-if="column.key === 'executionDuration'">
                    {{ record.executionDuration }} ms
                  </template>
                </template>
              </a-table>
            </a-tab-pane>
            <a-tab-pane key="entityChanges" :tab="`实体变更（${detail.entityChanges.length}）`">
              <a-table
                :columns="entityChangeColumns"
                :data-source="detail.entityChanges"
                row-key="id"
                size="small"
                :pagination="false"
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.key === 'changeType'">
                    <a-tag :color="changeTypeColor(record.changeType)">{{ changeTypeLabel(record.changeType) }}</a-tag>
                  </template>
                  <template v-else-if="column.key === 'entityTypeFullName'">
                    {{ record.entityTypeFullName ?? '—' }}
                  </template>
                </template>
                <template #expandedRowRender="{ record }">
                  <a-table
                    :columns="propertyChangeColumns"
                    :data-source="record.propertyChanges"
                    row-key="id"
                    size="small"
                    :pagination="false"
                  />
                </template>
              </a-table>
            </a-tab-pane>
          </a-tabs>
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
import type { AuditLogListItem, AuditLogDetail, AuditLogListParams, EntityChangeType } from '@shared/core/types'
import { useAppStore } from '@/stores/app'
import { formatDateTime, resolveAppConfigTimeZone } from '@shared/web/utils/format'
/** a-range-picker 值为 Dayjs 对象；本页仅调用 toISOString，不直接依赖 dayjs 类型包 */
interface RangeTimeValue {
  toISOString: () => string
}

const appStore = useAppStore()
/** 显示时区：应用配置 timing.timeZone（ABP 时钟为 UTC），缺省 Asia/Shanghai */
const displayTz = computed(() => resolveAppConfigTimeZone(appStore.appConfig))

const pageSize = 15
const page = ref(1)
const total = ref(0)
const loading = ref(false)
const refreshing = ref(false)
const logs = ref<AuditLogListItem[]>([])

const query = ref({
  userName: undefined as string | undefined,
  url: undefined as string | undefined,
  httpMethod: undefined as string | undefined,
  hasException: false,
})
const timeRange = ref<[RangeTimeValue, RangeTimeValue] | null>(null)

const filters: DataTableFilter[] = [
  { key: 'userName', type: 'input', placeholder: '搜索用户名', width: 240 },
  { key: 'url', type: 'input', placeholder: '搜索 URL', width: 240 },
  {
    key: 'httpMethod',
    type: 'select',
    placeholder: 'HTTP 方法',
    width: 120,
    options: ['GET', 'POST', 'PUT', 'DELETE', 'PATCH'],
  },
  { key: 'hasException', type: 'switch', label: '仅看异常' },
]

const columns: DataTableColumn[] = [
  { title: '序号', key: 'index', width: 70, minWidth: 60, resizable: true },
  { title: '操作人', dataIndex: 'operator.userName', key: 'userName', width: 110, minWidth: 100, resizable: true },
  { title: '应用', dataIndex: 'applicationName', key: 'applicationName', width: 120, minWidth: 100, resizable: true },
  { title: '时间', dataIndex: 'executionTime', key: 'executionTime', width: 160, minWidth: 150, resizable: true },
  { title: '耗时', dataIndex: 'executionDuration', key: 'executionDuration', width: 90, minWidth: 80, resizable: true },
  { title: '方法', dataIndex: 'httpMethod', key: 'httpMethod', width: 80, minWidth: 70, resizable: true },
  { title: '状态码', dataIndex: 'httpStatusCode', key: 'httpStatusCode', width: 80, minWidth: 70, resizable: true },
  { title: 'URL', dataIndex: 'url', key: 'url', width: 320, minWidth: 200, resizable: true, ellipsis: true },
  { title: '操作', key: 'action', width: 180, minWidth: 80, fixed: 'right', resizable: true },
]

const actionColumns = [
  { title: '服务', dataIndex: 'serviceName', key: 'serviceName' },
  { title: '方法', dataIndex: 'methodName', key: 'methodName' },
  { title: '参数', dataIndex: 'parameters', key: 'parameters', width: 200 },
  { title: '时间', dataIndex: 'executionTime', key: 'executionTime', width: 150 },
  { title: '耗时', dataIndex: 'executionDuration', key: 'executionDuration', width: 80 },
]

const entityChangeColumns = [
  { title: '变更类型', dataIndex: 'changeType', key: 'changeType', width: 90 },
  { title: '实体', dataIndex: 'entityTypeFullName', key: 'entityTypeFullName', width: 220, ellipsis: true },
  { title: '实体 ID', dataIndex: 'entityId', key: 'entityId' },
  { title: '时间', dataIndex: 'changeTime', key: 'changeTime', width: 150 },
]

const propertyChangeColumns = [
  { title: '属性', dataIndex: 'propertyName', key: 'propertyName' },
  { title: '原值', dataIndex: 'originalValue', key: 'originalValue' },
  { title: '新值', dataIndex: 'newValue', key: 'newValue' },
]

/** 参数字符串含 \uXXXX 转义（如 \u0022、\u4e2d）：优先 JSON 反序列化再序列化解码；
 *  字符串被截断等导致非法 JSON 时，退化为直接替换 \uXXXX 转义序列 */
function decodeParams(raw: string | null): string {
  if (!raw) return '—'
  try {
    return JSON.stringify(JSON.parse(raw))
  } catch {
    return raw.replace(/\\u([0-9a-fA-F]{4})/g, (_, hex: string) => String.fromCharCode(parseInt(hex, 16)))
  }
}

function methodColor(method: string): string {
  switch (method.toUpperCase()) {
    case 'GET': return 'blue'
    case 'POST': return 'green'
    case 'PUT': return 'gold'
    case 'DELETE': return 'red'
    default: return 'default'
  }
}

function statusColor(code: number): string {
  if (code >= 400) return 'red'
  if (code >= 300) return 'blue'
  return 'green'
}

function changeTypeLabel(t: EntityChangeType): string {
  return t === 0 ? '新增' : t === 1 ? '更新' : '删除'
}

function changeTypeColor(t: EntityChangeType): string {
  return t === 0 ? 'green' : t === 1 ? 'blue' : 'red'
}

let filterTimer: ReturnType<typeof setTimeout> | undefined

watch(query, () => {
  clearTimeout(filterTimer)
  filterTimer = setTimeout(() => {
    page.value = 1
    void fetchLogs()
  }, 300)
}, { deep: true })

watch(timeRange, () => {
  page.value = 1
  void fetchLogs()
})

async function fetchLogs(): Promise<void> {
  loading.value = true
  try {
    const params: AuditLogListParams = {
      skipCount: (page.value - 1) * pageSize,
      maxResultCount: pageSize,
      sorting: 'executionTime desc',
      userName: query.value.userName?.trim() || undefined,
      url: query.value.url?.trim() || undefined,
      httpMethod: query.value.httpMethod,
      hasException: query.value.hasException || undefined,
      startTime: timeRange.value?.[0]?.toISOString(),
      endTime: timeRange.value?.[1]?.toISOString(),
    }
    const res = await getAuditLogs(params)
    logs.value = res.items.map((item) => ({
      ...item,
      executionTime: formatDateTime(item.executionTime, displayTz.value),
    }))
    total.value = res.totalCount
  } catch {
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
    message.success('已刷新')
  } finally {
    refreshing.value = false
  }
}

const detailVisible = ref(false)
const detailLoading = ref(false)
const detail = ref<AuditLogDetail | null>(null)

async function openDetail(id: string): Promise<void> {
  detailVisible.value = true
  detailLoading.value = true
  detail.value = null
  try {
    const d = await getAuditLogDetail(id)
    const tz = displayTz.value
    detail.value = {
      ...d,
      executionTime: formatDateTime(d.executionTime, tz),
      actions: d.actions.map((a) => ({ ...a, executionTime: formatDateTime(a.executionTime, tz) })),
      entityChanges: d.entityChanges.map((c) => ({ ...c, changeTime: formatDateTime(c.changeTime, tz) })),
    }
  } catch {
    message.error('加载日志详情失败')
    detailVisible.value = false
  } finally {
    detailLoading.value = false
  }
}

onMounted(fetchLogs)
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

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

.drawer-tabs {
  margin-top: @spacing-base;
}
.drawer-tabs :deep(.ant-tabs-nav) {
  margin-bottom: @spacing-sm;
}
.drawer-tabs :deep(.ant-tabs-tab) {
  padding: 6px 10px;
}

.cell-wrap {
  word-break: break-all;
}

.params-cell {
  display: block;
  max-height: 160px;
  overflow-y: auto;
  white-space: pre-wrap;
  word-break: break-all;
  font-family: monospace;
  font-size: @font-size-xs;
}

.exception-pre {
  margin: 0;
  white-space: pre-wrap;
  word-break: break-all;
  font-family: monospace;
  font-size: @font-size-xs;
}
</style>
