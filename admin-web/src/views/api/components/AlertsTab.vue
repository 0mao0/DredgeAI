<template>
  <DataTable
    v-model:query="query"
    :columns="columns"
    :data-source="alerts"
    :pagination="{ pageSize: 15 }"
    :filters="filters"
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
  </DataTable>
</template>

<script setup lang="ts">
import { DataTable } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
import { computed } from 'vue'
import { formatNumber } from '@shared/core/utils/format'
import type { AlertRecord } from '../types'

const props = defineProps<{
  alerts: AlertRecord[]
  filter: 'all' | 'calls' | 'tokens'
}>()

const emit = defineEmits<{
  'update:filter': [value: 'all' | 'calls' | 'tokens']
}>()

const filters: DataTableFilter[] = [
  {
    key: 'filter',
    type: 'radio',
    options: [
      { value: 'all', label: '全部' },
      { value: 'calls', label: '调用超限' },
      { value: 'tokens', label: 'Token 超限' },
    ],
  },
]

const query = computed({
  get: () => ({ filter: props.filter }),
  set: (v: { filter: 'all' | 'calls' | 'tokens' }) => {
    if (v.filter !== props.filter) emit('update:filter', v.filter)
  },
})

const columns: DataTableColumn[] = [
  { title: '用户', dataIndex: 'userName', key: 'userName', width: 120, minWidth: 100, resizable: true },
  { title: '部门', dataIndex: 'department', key: 'department', width: 120, minWidth: 100, resizable: true },
  { title: '模型', dataIndex: 'modelName', key: 'modelName', width: 160, minWidth: 120, resizable: true },
  { title: '超限类型', key: 'type', width: 110, minWidth: 90, resizable: true },
  { title: '当前用量 / 限制', key: 'ratio', width: 180, minWidth: 140, resizable: true },
  { title: '时间', dataIndex: 'time', key: 'time', width: 160, minWidth: 140, resizable: true },
]
</script>
