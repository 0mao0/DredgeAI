<template>
  <a-drawer
    :open="open"
    title="上传任务"
    width="520px"
    @close="emit('update:open', false)"
  >
    <a-empty v-if="!tasks.length" description="暂无上传任务" image="simple" />
    <div v-else class="task-drawer">
      <div v-for="task in tasks" :key="task.id" class="task-drawer__item">
        <div class="task-drawer__head">
          <FilePdfOutlined class="task-drawer__icon" />
          <span class="task-drawer__name" :title="task.fileName">{{ task.fileName }}</span>
          <a-tag :color="taskTagColor(task.status)">{{ taskTagText(task.status) }}</a-tag>
          <AppButton
            v-if="task.status === 'preview_failed' || task.status === 'upload_failed'"
            variant="link"
            size="sm"
            @click="emit('retryTask', task.id)"
          >
            重试
          </AppButton>
        </div>
        <a-progress
          v-if="task.status === 'previewing' || task.status === 'uploading'"
          :percent="task.progress"
          size="small"
        />
        <a-alert v-if="task.error" type="error" :message="task.error" show-icon />
      </div>
    </div>
  </a-drawer>
</template>

<script setup lang="ts">
import { AppButton } from '@shared/web'
import { FilePdfOutlined } from '@ant-design/icons-vue'
import type { StandardUploadTask, UploadTaskStatus } from '../composables/useStandardUpload'

defineProps<{
  open: boolean
  tasks: StandardUploadTask[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'retryTask': [id: string]
}>()

function taskTagColor(status: UploadTaskStatus): string {
  if (status === 'previewing' || status === 'uploading') return 'blue'
  if (status === 'ready' || status === 'uploaded') return 'green'
  return 'red'
}

function taskTagText(status: UploadTaskStatus): string {
  switch (status) {
    case 'previewing': return '预读中'
    case 'ready': return '已就绪'
    case 'preview_failed': return '预读失败'
    case 'uploading': return '上传中'
    case 'uploaded': return '已完成'
    case 'upload_failed': return '上传失败'
  }
  return status
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.task-drawer {
  display: flex;
  flex-direction: column;
  gap: @spacing-sm;

  &__item {
    padding: @spacing-sm @spacing-base;
    background: @card-bg;
    border: 1px solid @border-color;
    border-radius: @radius-base;
  }

  &__head {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    min-width: 0;
  }

  &__icon {
    color: @danger;
    flex-shrink: 0;
  }

  &__name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: @text-primary;
  }
}
</style>
