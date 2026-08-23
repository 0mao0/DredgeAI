<template>
  <DataTable
    v-model:query="query"
    :columns="columns"
    :data-source="tasks"
    :loading="loading"
    :pagination="paginationProps"
    :filters="filters"
    row-key="id"
  >
    <template #bodyCell="{ column, record }: { column: { key: string }; record: DubbingTask }">
      <template v-if="column.key === 'text'">
        <a-tooltip :title="record.text">
          <span class="cell-text">{{ record.text.slice(0, 40) }}{{ record.text.length > 40 ? '...' : '' }}</span>
        </a-tooltip>
      </template>
      <template v-if="column.key === 'user'">
        {{ record.userName || '-' }}
        <span v-if="record.department" class="cell-dept">({{ record.department }})</span>
      </template>
      <template v-if="column.key === 'status'">
        <a-tag :color="statusColor(record.status)">{{ record.status }}</a-tag>
      </template>
      <template v-if="column.key === 'duration'">
        {{ record.durationSec ? `${record.durationSec}s` : '-' }} / {{ record.tokenCost }}
      </template>
      <template v-if="column.key === 'deletedByUser'">
        <a-tag :color="record.deletedByUser ? 'red' : 'green'">{{ record.deletedByUser ? '用户已删除' : '保留中' }}</a-tag>
      </template>
      <template v-if="column.key === 'createdAt'">
        {{ formatTime(record.createdAt) }}
      </template>
      <template v-if="column.key === 'actions'">
        <AppButton variant="link" size="sm" :disabled="!(record.status === '已完成' && record.audioUrl)" @click="emit('play', record)">
          <PlayCircleOutlined /> 播放
        </AppButton>
        <a-tooltip :title="!record.deletedByUser ? '用户未删除，受隐私限制不可彻底删除' : ''">
          <AppButton variant="link" size="sm" danger :disabled="!record.deletedByUser" @click="emit('delete', record.id)">
            <DeleteOutlined /> 删除
          </AppButton>
        </a-tooltip>
      </template>
    </template>
  </DataTable>
</template>

<script setup lang="ts">
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn, DataTableFilter } from '@shared/web'
import { computed, ref, watch } from 'vue'
import { PlayCircleOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import type { DubbingTask } from '@/types'

const props = defineProps<{ tasks: DubbingTask[], loading: boolean }>()
const emit = defineEmits<{
  play: [task: DubbingTask]
  delete: [id: string]
  search: [filters: { keyword: string, status: string | undefined, deletedOnly: boolean }]
}>()

const query = ref({ keyword: '', status: undefined as string | undefined, deletedOnly: false })
const filters: DataTableFilter[] = [
  { key: 'keyword', type: 'input', placeholder: '搜索用户 / 文本', width: 240 },
  { key: 'status', type: 'select', placeholder: '状态', width: 140, options: ['生成中', '已完成', '已失败'] },
  { key: 'deletedOnly', type: 'switch', label: '仅看用户已删除' },
]

watch(query, () => {
  emit('search', { ...query.value })
}, { deep: true })

const paginationProps = computed(() => ({
  pageSize: 10,
  showTotal: (total: number) => `共 ${total} 条`,
  hideOnSinglePage: props.tasks.length <= 10,
}))

const columns: DataTableColumn[] = [
  { title: '用户', key: 'user', width: 160, minWidth: 120, resizable: true },
  { title: '文本', key: 'text', width: 240, minWidth: 200, resizable: true },
  { title: '配音音色', key: 'voice', dataIndex: 'voiceName', width: 120, minWidth: 100, resizable: true },
  { title: '状态', key: 'status', width: 100, minWidth: 80, resizable: true },
  { title: '时长 / Token', key: 'duration', width: 140, minWidth: 110, resizable: true },
  { title: '用户删除状态', key: 'deletedByUser', width: 140, minWidth: 110, resizable: true },
  { title: '创建时间', key: 'createdAt', dataIndex: 'createdAt', width: 160, minWidth: 140, resizable: true },
  { title: '操作', key: 'actions', width: 160, minWidth: 160, fixed: 'right', resizable: true },
]

function statusColor(status: string): string {
  switch (status) {
    case '已完成': return 'green'
    case '生成中': return 'blue'
    case '已失败': return 'red'
    default: return 'default'
  }
}

function formatTime(iso: string): string {
  const d = new Date(iso)
  const y = d.getFullYear()
  const mo = String(d.getMonth() + 1).padStart(2, '0')
  const da = String(d.getDate()).padStart(2, '0')
  const h = String(d.getHours()).padStart(2, '0')
  const mi = String(d.getMinutes()).padStart(2, '0')
  return `${y}-${mo}-${da} ${h}:${mi}`
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.cell-text {
  display: inline-block;
  max-width: 260px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  vertical-align: middle;
}
.cell-dept {
  color: @text-tertiary;
  font-size: 12px;
}
</style>
