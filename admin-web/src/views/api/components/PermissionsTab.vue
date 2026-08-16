<template>
  <SectionCard nopad>
    <div class="user-filter-bar">
      <a-input-search
        :value="keyword"
        placeholder="搜索姓名 / 部门"
        allow-clear
        style="width:200px"
        @update:value="emit('update:keyword', $event)"
      />
      <a-switch
        :checked="partialOnly"
        checked-children="部分权限"
        un-checked-children="全部权限"
        class="perm-switch"
        @update:checked="emit('update:partialOnly', $event)"
      />
    </div>
    <a-table
      size="small"
      :data-source="users"
      :columns="permissionColumns"
      :pagination="{ pageSize: 15, showTotal: (t: number) => `共 ${t} 人` }"
      row-key="userId"
      :locale="{ emptyText: '暂无数据' }"
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
          <a-button type="link" size="small" @click="emit('editLimits', record)">编辑</a-button>
        </template>
      </template>
    </a-table>
  </SectionCard>

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
import SectionCard from '@shared/web/components/SectionCard.vue'
import { formatLimit } from '../utils'
import type { MergedUserRecord, ModelLimitEntry } from '../types'

defineProps<{
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

const permissionColumns = [
  { title: '序号', key: 'index', width: 70 },
  { title: '用户', dataIndex: 'name', key: 'name', width: 100 },
  { title: '部门', dataIndex: 'department', key: 'department', width: 100 },
  { title: '已授权模型', key: 'models', width: 120 },
  { title: '调用限制', key: 'callsLimit', width: 120 },
  { title: 'Token 限制', key: 'tokensLimit', width: 120 },
  { title: '操作', key: 'action', width: 100 },
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
.perm-switch {
  margin-left: auto;
}
</style>
