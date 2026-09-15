<template>
  <DataTable
    :columns="columns"
    :data-source="routes"
    row-key="id"
    :loading="loading"
    :pagination="{ current: page, pageSize: 15, total }"
    :filters="filters"
    :query="query"
    storage-key="admin-gateway-routes"
    @update:query="emit('update:query', $event)"
    @change="(p: unknown) => emit('change', p)"
  >
    <template #toolbarExtra>
      <AppButton v-if="can('createRoute')" variant="primary" size="sm" @click="emit('create')">
        新增路由
      </AppButton>
    </template>
    <template #bodyCell="{ column, record }">
      <template v-if="column.key === 'match'">
        <div class="match-cell">
          <span class="match-cell__path">{{ record.match.path || '-' }}</span>
          <a-tag v-for="h in record.match.hosts ?? []" :key="h" class="match-cell__host">{{ h }}</a-tag>
        </div>
      </template>
      <template v-else-if="column.key === 'authorizationPolicy'">
        <span>{{ record.authorizationPolicy || '-' }}</span>
      </template>
      <template v-else-if="column.key === 'isEnabled'">
        <a-tag :color="record.isEnabled ? 'green' : 'red'">{{ record.isEnabled ? '启用' : '禁用' }}</a-tag>
      </template>
      <template v-else-if="column.key === 'action'">
        <div class="routes-tab__actions">
          <AppButton v-if="can('updateRoute')" variant="link" size="sm" @click="emit('edit', record)">编辑</AppButton>
          <a-popconfirm
            v-if="can('deleteRoute')"
            title="确认删除该路由？保存后即时生效"
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
import type { ProxyRouteItem } from '@/api/modules/gateway'

const props = defineProps<{
  routes: ProxyRouteItem[]
  loading: boolean
  total: number
  page: number
  query: { keyword?: string, clusterId?: string }
  clusterOptions: Array<{ value: string, label: string }>
  can: (action: string) => boolean
}>()

const emit = defineEmits<{
  'update:query': [q: { keyword?: string, clusterId?: string }]
  'change': [pagination: unknown]
  'create': []
  'edit': [record: ProxyRouteItem]
  'remove': [record: ProxyRouteItem]
}>()

const filters = computed<DataTableFilter[]>(() => [
  { key: 'keyword', type: 'input', placeholder: '搜索路由 ID 或配置', width: 240 },
  { key: 'clusterId', type: 'select', placeholder: '目标集群', width: 160, options: props.clusterOptions },
])

const columns = computed<DataTableColumn[]>(() => {
  const base: DataTableColumn[] = [
    { title: '路由 ID', dataIndex: 'routeId', key: 'routeId', width: 200, minWidth: 140, resizable: true },
    { title: '匹配规则', key: 'match', width: 320, minWidth: 200, resizable: true, flex: true },
    { title: '目标集群', dataIndex: 'clusterId', key: 'clusterId', width: 180, minWidth: 120, resizable: true },
    { title: '优先级', dataIndex: 'order', key: 'order', width: 70, minWidth: 60 },
    { title: '授权策略', key: 'authorizationPolicy', width: 140, minWidth: 100, resizable: true },
    { title: '状态', dataIndex: 'isEnabled', key: 'isEnabled', width: 80, minWidth: 70 },
    { title: '操作', key: 'action', width: 180, minWidth: 120, fixed: 'right' },
  ]
  return props.can('updateRoute') || props.can('deleteRoute') ? base : base.filter((c) => c.key !== 'action')
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.match-cell {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 4px;

  &__path {
    font-size: @font-size-xs;
  }

  &__host {
    font-size: @font-size-xs;
    margin-inline-end: 0;
  }
}

.routes-tab__actions {
  display: flex;
  align-items: center;
  gap: 6px;
  white-space: nowrap;
}
</style>
