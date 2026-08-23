<template>
  <DataTable
    :columns="columns"
    :data-source="tasks"
    :loading="loading"
    :pagination="{ pageSize: 5, hideOnSinglePage: tasks.length <= 5 }"
    row-key="id"
    :card="false"
    class="history-table"
  >
    <template #bodyCell="{ column, record, index }">
      <template v-if="column.key === 'index'">
        {{ index + 1 }}
      </template>
      <template v-else-if="column.key === 'text'">
        <a-tooltip :title="record.text">
          <span>{{ record.text.length > 25 ? `${record.text.slice(0, 25)}...` : record.text }}</span>
        </a-tooltip>
      </template>
      <template v-else-if="column.key === 'status'">
        <a-tag :color="statusColor(record.status)">{{ record.status }}</a-tag>
      </template>
      <template v-else-if="column.key === 'duration'">
        {{ record.durationSec ? `${record.durationSec}s` : '-' }}
      </template>
      <template v-else-if="column.key === 'createdAt'">
        {{ formatTime(record.createdAt) }}
      </template>
      <template v-else-if="column.key === 'action'">
        <div class="history-actions">
          <AppButton
            v-if="record.status === '已完成' && record.audioUrl"
            variant="link"
            size="sm"
            class="history-actions__btn"
            @click="emit('play', record)"
          >
            播放
          </AppButton>
          <a-tooltip v-else-if="record.status === '已完成'" title="刷新后音频已失效，重新生成">
            <AppButton
              variant="link"
              size="sm"
              class="history-actions__btn"
              :loading="regeneratingId === record.id"
              @click="emit('regenerate', record)"
            >
              重新生成
            </AppButton>
          </a-tooltip>
          <a-tooltip title="载入文本与音色，重新编辑">
            <AppButton variant="link" size="sm" class="history-actions__btn" @click="emit('reEdit', record)">
              编辑
            </AppButton>
          </a-tooltip>
          <a-popconfirm title="确定删除此任务？" @confirm="emit('delete', record.id)">
            <AppButton variant="link" danger size="sm" class="history-actions__btn">删除</AppButton>
          </a-popconfirm>
        </div>
      </template>
    </template>
    <template #emptyText>
      <div class="history-empty">
        <CustomerServiceOutlined class="history-empty__icon" />
        <p>暂无配音记录，生成后将显示在这里</p>
      </div>
    </template>
  </DataTable>
</template>

<script setup lang="ts">
import { AppButton, DataTable } from '@shared/web'
import type { DataTableColumn } from '@shared/web'
import { CustomerServiceOutlined } from '@ant-design/icons-vue'
import type { DubbingTask } from '@/types'

const props = defineProps<{
  tasks: DubbingTask[]
  loading: boolean
  regeneratingId?: string | null
}>()

const emit = defineEmits<{
  play: [task: DubbingTask]
  delete: [id: string]
  reEdit: [task: DubbingTask]
  regenerate: [task: DubbingTask]
}>()

const columns: DataTableColumn[] = [
  { title: '序号', key: 'index', width: 60 },
  // 文本列作为弹性列吸收剩余宽度，其余列保持合适默认宽
  { title: '文本', dataIndex: 'text', key: 'text', ellipsis: true, width: 260, minWidth: 180, resizable: true, flex: true },
  { title: '声音', dataIndex: 'voiceName', key: 'voiceName', width: 90, minWidth: 80, resizable: true },
  { title: '状态', key: 'status', width: 90, minWidth: 80, resizable: true },
  { title: '时长', key: 'duration', width: 90, minWidth: 80, resizable: true },
  { title: 'Token', dataIndex: 'tokenCost', key: 'tokenCost', width: 90, minWidth: 80, resizable: true },
  // 时间列紧邻固定右侧的操作列，按标准页惯例不参与拖拽（fixed-right 浮层会盖住其手柄）
  { title: '时间', key: 'createdAt', width: 130, minWidth: 110 },
  { title: '操作', key: 'action', width: 230, minWidth: 210, fixed: 'right', resizable: true },
]

function statusColor(status: string): string {
  if (status === '生成中') return 'blue'
  if (status === '已完成') return 'green'
  if (status === '已失败') return 'red'
  return 'default'
}

function formatTime(iso: string): string {
  const d = new Date(iso)
  const mo = String(d.getMonth() + 1).padStart(2, '0')
  const da = String(d.getDate()).padStart(2, '0')
  const h = String(d.getHours()).padStart(2, '0')
  const mi = String(d.getMinutes()).padStart(2, '0')
  return `${mo}-${da} ${h}:${mi}`
}

void props
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.history-table {
  :deep(.ant-table-tbody > tr) {
    transition: background 0.2s ease;
    cursor: default;
  }
  :deep(.ant-table-tbody > tr:hover > td) {
    background: color-mix(in srgb, @brand-primary 5%, transparent);
  }
}

.history-empty {
  padding: @spacing-lg 0;
  &__icon { font-size: 28px; color: @text-tertiary; margin-bottom: @spacing-xs; }
  p { font-size: @font-size-sm; color: @text-tertiary; margin: 0; }
}

.history-actions {
  display: flex;
  align-items: center;
  flex-wrap: nowrap;
  justify-content: center;
  gap: 2px 10px;
  &__btn { padding: 0 2px; }
}
</style>
