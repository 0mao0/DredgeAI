<template>
  <div class="history-panel">
    <SectionCard title="历史记录" class="history-panel__card">
      <template #extra>
        <span class="history-panel__count">共 {{ tasks.length }} 条</span>
      </template>
      <div class="history-panel__body">
        <DubbingHistoryTable
          :tasks="tasks"
          :loading="loading"
          @play="emit('play', $event)"
          @delete="emit('delete', $event)"
          @re-edit="emit('reEdit', $event)"
        />
      </div>
    </SectionCard>
  </div>
</template>

<script setup lang="ts">
import SectionCard from '@shared/web/components/SectionCard.vue'
import DubbingHistoryTable from './DubbingHistoryTable.vue'
import type { DubbingTask } from '@/types'

const props = defineProps<{
  tasks: DubbingTask[]
  loading: boolean
}>()

const emit = defineEmits<{
  play: [task: DubbingTask]
  delete: [id: string]
  reEdit: [task: DubbingTask]
}>()

void props
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.history-panel {
  height: 100%;
  display: flex;
  flex-direction: column;
  min-height: 0;

  :deep(.section-card-header) {
    padding: @spacing-lg @spacing-xl @spacing-sm;
  }

  &__card {
    height: 100%;
    display: flex;
    flex-direction: column;
    min-height: 0;
    :deep(.section-card-body) {
      flex: 1;
      min-height: 0;
      overflow: hidden;
      padding: 0;
    }
  }
  &__count { font-size: @font-size-xs; color: @text-tertiary; }
  &__body {
    height: 100%;
    min-height: 0;
    overflow-y: auto;
    padding: @spacing-md @spacing-xl @spacing-xl;
  }
}
</style>
