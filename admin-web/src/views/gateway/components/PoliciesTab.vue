<template>
  <DataTable
    :columns="columns"
    :data-source="policies"
    row-key="id"
    :loading="loading"
    :pagination="{ current: page, pageSize: 15, total }"
    :filters="filters"
    :query="query"
    storage-key="admin-gateway-policies"
    @update:query="emit('update:query', $event)"
    @change="(p: unknown) => emit('change', p)"
  >
    <template #toolbarExtra>
      <AppButton v-if="can('createPolicy')" variant="primary" size="sm" @click="emit('create')">
        新增策略
      </AppButton>
    </template>
    <template #bodyCell="{ column, record }">
      <template v-if="column.key === 'scope'">
        <a-tag :color="record.scope === 0 ? 'blue' : 'green'">
          {{ record.scope === 0 ? '全局' : '路由级' }}
        </a-tag>
      </template>
      <template v-else-if="column.key === 'routeId'">
        <span>{{ record.scope === 0 ? '-' : record.routeId }}</span>
      </template>
      <template v-else-if="column.key === 'algorithm'">
        <span>{{ ALGORITHM_LABELS[record.algorithm as RateLimitAlgorithmValue] ?? record.algorithm }}</span>
      </template>
      <template v-else-if="column.key === 'params'">
        <span>{{ formatParams(record) }}</span>
      </template>
      <template v-else-if="column.key === 'isEnabled'">
        <a-tag :color="record.isEnabled ? 'green' : 'red'">{{ record.isEnabled ? '启用' : '禁用' }}</a-tag>
      </template>
      <template v-else-if="column.key === 'action'">
        <div class="policies-tab__actions">
          <AppButton v-if="can('updatePolicy')" variant="link" size="sm" @click="emit('edit', record)">编辑</AppButton>
          <a-popconfirm
            v-if="can('deletePolicy')"
            title="确认删除该策略？保存后即时生效"
            placement="left"
            @confirm="emit('remove', record)"
          >
            <AppButton variant="link" size="sm" danger>删除</AppButton>
          </a-popconfirm>
        </div>
      </template>
    </template>
  </DataTable>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
import type { RateLimitAlgorithmValue, RateLimitPolicyItem } from '@/api/modules/gateway'

const props = defineProps<{
  policies: RateLimitPolicyItem[]
  loading: boolean
  total: number
  page: number
  query: { keyword?: string, scope?: string, routeId?: string }
  routeOptions: Array<{ value: string, label: string }>
  can: (action: string) => boolean
}>()

const emit = defineEmits<{
  'update:query': [q: { keyword?: string, scope?: string, routeId?: string }]
  'change': [pagination: unknown]
  'create': []
  'edit': [record: RateLimitPolicyItem]
  'remove': [record: RateLimitPolicyItem]
}>()

const SCOPE_OPTIONS = [
  { value: '0', label: '全局' },
  { value: '1', label: '路由级' },
]

const ALGORITHM_LABELS: Record<RateLimitAlgorithmValue, string> = {
  0: '固定窗口',
  1: '滑动窗口',
  2: '令牌桶',
}

/** 限流参数摘要：按算法展示对应参数组合 */
function formatParams(record: RateLimitPolicyItem): string {
  if (record.algorithm === 2) {
    return `容量 ${record.tokenLimit ?? '-'} · 每 ${record.replenishmentPeriodSeconds ?? '-'} 秒补 ${record.tokensPerPeriod ?? '-'}`
  }
  const base = `${record.permitLimit ?? '-'} 次 / ${record.windowSeconds ?? '-'} 秒`
  return record.algorithm === 1 ? `${base} · ${record.segmentsPerWindow ?? '-'} 段` : base
}

const filters = computed<DataTableFilter[]>(() => [
  { key: 'keyword', type: 'input', placeholder: '搜索名称或路由 ID', width: 240 },
  { key: 'scope', type: 'select', placeholder: '作用域', width: 160, options: SCOPE_OPTIONS },
  { key: 'routeId', type: 'select', placeholder: '目标路由', width: 180, options: props.routeOptions },
])

const columns = computed<DataTableColumn[]>(() => {
  const base: DataTableColumn[] = [
    { title: '名称', dataIndex: 'name', key: 'name', width: 160, minWidth: 120, resizable: true },
    { title: '作用域', key: 'scope', width: 90, minWidth: 80 },
    { title: '目标路由', dataIndex: 'routeId', key: 'routeId', width: 180, minWidth: 120, resizable: true },
    { title: '算法', key: 'algorithm', width: 110, minWidth: 90 },
    { title: '限流参数', key: 'params', minWidth: 200, resizable: true, flex: true },
    { title: '排队', dataIndex: 'queueLimit', key: 'queueLimit', width: 70, minWidth: 60 },
    { title: '状态', dataIndex: 'isEnabled', key: 'isEnabled', width: 80, minWidth: 70 },
    { title: '操作', key: 'action', width: 180, minWidth: 120, fixed: 'right' },
  ]
  return props.can('updatePolicy') || props.can('deletePolicy') ? base : base.filter((c) => c.key !== 'action')
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.policies-tab__actions {
  display: flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
}
</style>
