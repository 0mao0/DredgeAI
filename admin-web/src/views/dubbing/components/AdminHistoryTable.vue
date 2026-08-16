<template>
  <a-table
    :data-source="tasks"
    :columns="columns"
    :loading="loading"
    :pagination="paginationProps"
    row-key="id"
    size="small"
    :scroll="{ x: 1100 }"
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
        <a-button type="link" size="small" :disabled="!(record.status === '已完成' && record.audioUrl)" @click="emit('play', record)">
          <PlayCircleOutlined /> 播放
        </a-button>
        <a-tooltip :title="!record.deletedByUser ? '用户未删除，受隐私限制不可彻底删除' : ''">
          <a-button type="link" size="small" danger :disabled="!record.deletedByUser" @click="emit('delete', record.id)">
            <DeleteOutlined /> 删除
          </a-button>
        </a-tooltip>
      </template>
    </template>
  </a-table>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { PlayCircleOutlined, DeleteOutlined } from '@ant-design/icons-vue'
import type { DubbingTask } from '@/types'

const props = defineProps<{ tasks: DubbingTask[], loading: boolean }>()
const emit = defineEmits<{ play: [task: DubbingTask], delete: [id: string] }>()

const paginationProps = computed(() => ({
  pageSize: 10,
  showTotal: (total: number) => `共 ${total} 条`,
  hideOnSinglePage: props.tasks.length <= 10,
}))

const columns = [
  { title: '用户', key: 'user', width: 160 },
  { title: '文本', key: 'text', ellipsis: true, width: 240 },
  { title: '配音音色', key: 'voice', dataIndex: 'voiceName', width: 120 },
  { title: '状态', key: 'status', width: 100 },
  { title: '时长 / Token', key: 'duration', width: 140 },
  { title: '用户删除状态', key: 'deletedByUser', width: 140 },
  { title: '创建时间', key: 'createdAt', dataIndex: 'createdAt', width: 160 },
  { title: '操作', key: 'actions', width: 160, fixed: 'right' },
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
