<template>
  <SectionCard nopad>
    <template #extra>
      <a-radio-group :value="filter" size="small" button-style="solid" @update:value="emit('update:filter', $event)">
        <a-radio-button value="all">全部</a-radio-button>
        <a-radio-button value="calls">调用超限</a-radio-button>
        <a-radio-button value="tokens">Token 超限</a-radio-button>
      </a-radio-group>
    </template>
    <a-table
      size="small"
      :data-source="alerts"
      :columns="alertColumns"
      :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 条` }"
      row-key="id"
      :locale="{ emptyText: '暂无数据' }"
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
</template>

<script setup lang="ts">
import SectionCard from '@shared/web/components/SectionCard.vue'
import { formatNumber } from '@shared/core/utils/format'
import type { AlertRecord } from '../types'

defineProps<{
  alerts: AlertRecord[]
  filter: 'all' | 'calls' | 'tokens'
}>()

const emit = defineEmits<{
  'update:filter': [value: 'all' | 'calls' | 'tokens']
}>()

const alertColumns = [
  { title: '用户', dataIndex: 'userName', key: 'userName' },
  { title: '部门', dataIndex: 'department', key: 'department' },
  { title: '模型', dataIndex: 'modelName', key: 'modelName' },
  { title: '超限类型', key: 'type', width: 110 },
  { title: '当前用量 / 限制', key: 'ratio' },
  { title: '时间', dataIndex: 'time', key: 'time', width: 160 },
]
</script>
