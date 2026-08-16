<template>
  <a-table
    :data-source="tasks"
    :loading="loading"
    :pagination="paginationProps"
    row-key="id"
    size="small"
    :scroll="{ x: 694 }"
    class="history-table"
  >
    <a-table-column title="文本" data-index="text" ellipsis width="180">
      <template #default="{ record }">
        <a-tooltip :title="record.text">
          <span>{{ record.text.length > 25 ? `${record.text.slice(0, 25)}...` : record.text }}</span>
        </a-tooltip>
      </template>
    </a-table-column>
    <a-table-column title="声音" data-index="voiceName" width="56" align="center" />
    <a-table-column title="状态" data-index="status" width="50" align="center">
      <template #default="{ record }">
        <a-tag :color="statusColor(record.status)">{{ record.status }}</a-tag>
      </template>
    </a-table-column>
    <a-table-column title="时长" width="42" align="center">
      <template #default="{ record }">
        {{ record.durationSec ? `${record.durationSec}s` : '-' }}
      </template>
    </a-table-column>
    <a-table-column title="Token" data-index="tokenCost" width="44" align="center" />
    <a-table-column title="时间" data-index="createdAt" width="92" align="center">
      <template #default="{ record }">
        {{ formatTime(record.createdAt) }}
      </template>
    </a-table-column>
    <a-table-column title="操作" width="230" fixed="right">
      <template #default="{ record }">
        <div class="history-actions">
          <a-button
            v-if="record.status === '已完成' && record.audioUrl"
            type="link"
            size="small"
            class="history-actions__btn"
            @click="emit('play', record)"
          >
            播放
          </a-button>
          <a-tooltip v-else-if="record.status === '已完成'" title="刷新后音频已失效，重新生成">
            <a-button
              type="link"
              size="small"
              class="history-actions__btn"
              :loading="regeneratingId === record.id"
              @click="emit('regenerate', record)"
            >
              重新生成
            </a-button>
          </a-tooltip>
          <a-tooltip title="载入文本与音色，重新编辑">
            <a-button type="link" size="small" class="history-actions__btn" @click="emit('reEdit', record)">
              编辑
            </a-button>
          </a-tooltip>
          <a-popconfirm title="确定删除此任务？" @confirm="emit('delete', record.id)">
            <a-button type="link" danger size="small" class="history-actions__btn">删除</a-button>
          </a-popconfirm>
        </div>
      </template>
    </a-table-column>
    <template #emptyText>
      <div class="history-empty">
        <CustomerServiceOutlined class="history-empty__icon" />
        <p>暂无配音记录，生成后将显示在这里</p>
      </div>
    </template>
  </a-table>
</template>

<script setup lang="ts">
import { computed } from 'vue'
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

const paginationProps = computed(() => ({
  pageSize: 5,
  showSizeChanger: false,
  showTotal: (total: number) => `共 ${total} 条`,
  hideOnSinglePage: props.tasks.length <= 5,
}))
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
