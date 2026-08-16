<template>
  <SectionCard nopad>
    <div class="user-filter-bar">
      <a-input-search
        :value="userKeyword"
        placeholder="搜索用户"
        allow-clear
        style="width:180px"
        @update:value="emit('update:userKeyword', $event)"
      />
      <a-select
        :value="modelFilter"
        mode="multiple"
        allow-clear
        placeholder="模型"
        :max-tag-count="0"
        :max-tag-placeholder="modelFilter.length ? `已选 ${modelFilter.length}` : '全部'"
        style="width:140px"
        @update:value="emit('update:modelFilter', $event)"
      >
        <a-select-option v-for="m in allModelNames" :key="m" :value="m">{{ m }}</a-select-option>
      </a-select>
      <a-select :value="statusFilter" allow-clear placeholder="状态" size="small" style="width:100px" @update:value="emit('update:statusFilter', $event)">
        <a-select-option value="成功">成功</a-select-option>
        <a-select-option value="失败">失败</a-select-option>
      </a-select>
    </div>
    <a-table
      size="small"
      :data-source="records"
      :columns="callColumns"
      :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 条` }"
      row-key="id"
      :locale="{ emptyText: '暂无数据' }"
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
</template>

<script setup lang="ts">
import SectionCard from '@shared/web/components/SectionCard.vue'
import type { CallRecord } from '../types'

defineProps<{
  records: CallRecord[]
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

const callColumns = [
  { title: '时间', dataIndex: 'time', key: 'time', width: 160 },
  { title: '模型', dataIndex: 'modelName', key: 'modelName' },
  { title: '用户', key: 'userName', width: 100 },
  { title: '输入 Token', key: 'inputTokens', width: 100 },
  { title: '输出 Token', key: 'outputTokens', width: 100 },
  { title: '延迟', key: 'latency', width: 90 },
  { title: '状态', key: 'status', width: 80 },
]
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.user-filter-bar {
  display: flex;
  gap: @spacing-sm;
  align-items: center;
  flex-wrap: wrap;
  padding: 0;
  margin-bottom: @spacing-base;

  :deep(.ant-input-group-wrapper) {
    display: inline-flex;
    align-items: center;
    vertical-align: middle;
  }
  :deep(.ant-input-search-button) {
    display: inline-flex;
    align-items: center;
    justify-content: center;
  }
}
</style>
