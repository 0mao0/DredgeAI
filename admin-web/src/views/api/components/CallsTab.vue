<template>
  <DataTable
    v-model:query="query"
    :columns="columns"
    :data-source="records"
    :loading="loading"
    :pagination="{ pageSize: 15 }"
    :filters="filters"
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
</template>

<script setup lang="ts">
import { DataTable } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
import { computed } from 'vue'
import type { CallRecord } from '../types'

const props = defineProps<{
  records: CallRecord[]
  loading?: boolean
  allModelNames: string[]
  userKeyword: string
  modelFilter: string[]
  statusFilter: string | undefined
}>()

const emit = defineEmits<{
  'update:userKeyword': [value: string]
  'update:modelFilter': [value: string[]]
  'update:statusFilter': [value: string | undefined]
}>()

const filters: DataTableFilter[] = [
  { key: 'userKeyword', type: 'input', placeholder: '搜索用户', width: 180 },
  { key: 'modelFilter', type: 'select', multiple: true, placeholder: '模型', width: 160, options: props.allModelNames },
  { key: 'statusFilter', type: 'select', placeholder: '状态', width: 100, options: ['成功', '失败'] },
]

const query = computed({
  get: () => ({ userKeyword: props.userKeyword, modelFilter: props.modelFilter, statusFilter: props.statusFilter }),
  set: (v: { userKeyword: string, modelFilter: string[], statusFilter: string | undefined }) => {
    if (v.userKeyword !== props.userKeyword) emit('update:userKeyword', v.userKeyword)
    if (v.modelFilter !== props.modelFilter) emit('update:modelFilter', v.modelFilter)
    if (v.statusFilter !== props.statusFilter) emit('update:statusFilter', v.statusFilter)
  },
})

const columns: DataTableColumn[] = [
  { title: '时间', dataIndex: 'time', key: 'time', width: 160, minWidth: 140, resizable: true },
  { title: '模型', dataIndex: 'modelName', key: 'modelName', width: 180, minWidth: 120, resizable: true },
  { title: '用户', key: 'userName', width: 100, minWidth: 90, resizable: true },
  { title: '输入 Token', key: 'inputTokens', width: 100, minWidth: 90, resizable: true },
  { title: '输出 Token', key: 'outputTokens', width: 100, minWidth: 90, resizable: true },
  { title: '延迟', key: 'latency', width: 90, minWidth: 80, resizable: true },
  { title: '状态', key: 'status', width: 80, minWidth: 70, resizable: true },
]
</script>
