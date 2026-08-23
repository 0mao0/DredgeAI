<template>
  <DataTable
    v-model:query="query"
    :columns="columns"
    :data-source="users"
    :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 人` }"
    :filters="filters"
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
        <AppButton variant="link" size="sm" @click="emit('editLimits', record)">编辑</AppButton>
      </template>
    </template>
  </DataTable>

  <!-- Edit Limits Modal -->
  <a-modal :open="limitsOpen" title="编辑用户限制" width="820px" @update:open="emit('update:limitsOpen', $event)" @ok="emit('limitsOk')">
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
</template>

<script setup lang="ts">
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
import { computed } from 'vue'
import { formatLimit } from '../utils'
import type { MergedUserRecord, ModelLimitEntry } from '../types'

const props = defineProps<{
  users: MergedUserRecord[]
  allModelNames: string[]
  keyword: string
  partialOnly: boolean
  limitsOpen: boolean
  limitsTarget: { userId: string, name: string, modelLimits: ModelLimitEntry[] } | null
}>()

const emit = defineEmits<{
  'update:keyword': [value: string]
  'update:partialOnly': [value: boolean]
  'update:limitsOpen': [value: boolean]
  'editLimits': [record: MergedUserRecord]
  'limitsOk': []
}>()

const limitsForm = defineModel<ModelLimitEntry[]>('limitsForm', { required: true })

const filters: DataTableFilter[] = [
  { key: 'keyword', type: 'input', placeholder: '搜索姓名 / 部门', width: 200 },
  { key: 'partialOnly', type: 'switch', checkedLabel: '部分权限', uncheckedLabel: '全部权限' },
]

const query = computed({
  get: () => ({ keyword: props.keyword, partialOnly: props.partialOnly }),
  set: (v: { keyword: string, partialOnly: boolean }) => {
    if (v.keyword !== props.keyword) emit('update:keyword', v.keyword)
    if (v.partialOnly !== props.partialOnly) emit('update:partialOnly', v.partialOnly)
  },
})

const columns: DataTableColumn[] = [
  { title: '序号', key: 'index', width: 70, minWidth: 60, resizable: true },
  { title: '用户', dataIndex: 'name', key: 'name', width: 100, minWidth: 90, resizable: true },
  { title: '部门', dataIndex: 'department', key: 'department', width: 100, minWidth: 90, resizable: true },
  { title: '已授权模型', key: 'models', width: 120, minWidth: 100, resizable: true },
  { title: '调用限制', key: 'callsLimit', width: 120, minWidth: 100, resizable: true },
  { title: 'Token 限制', key: 'tokensLimit', width: 120, minWidth: 100, resizable: true },
  { title: '操作', key: 'action', width: 100, minWidth: 100, fixed: 'right', resizable: true },
]

const limitColumns = [
  { title: '启用', key: 'enabled', width: 60 },
  { title: '模型', key: 'modelName', width: 130 },
  { title: '调用次限制', key: 'callsLimit', width: 150 },
  { title: '调用次预警', key: 'callsWarn', width: 150 },
  { title: 'Token 限制', key: 'tokensLimit', width: 150 },
  { title: 'Token 预警', key: 'tokensWarn', width: 150 },
]
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

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
</style>
