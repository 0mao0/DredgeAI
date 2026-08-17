<template>
  <a-modal
    :open="open"
    title="上传标准文档"
    width="800px"
    :footer="null"
    @cancel="emit('update:open', false)"
  >
    <div class="upload-modal">
      <a-upload-dragger
        multiple
        accept=".pdf,application/pdf"
        :show-upload-list="false"
        :before-upload="handleBeforeUpload"
      >
        <p class="ant-upload-drag-icon">
          <InboxOutlined />
        </p>
        <p class="ant-upload-text">点击或拖拽 PDF 文件到此区域</p>
        <p class="ant-upload-hint">支持 .pdf，单个不超过 50MB，单批最多 10 个</p>
      </a-upload-dragger>

      <a-collapse v-if="tasks.length" v-model:active-key="activeKey" class="upload-modal__list">
        <a-collapse-panel v-for="task in tasks" :key="task.id">
          <template #header>
            <div class="upload-modal__row">
              <FilePdfOutlined class="upload-modal__icon" />
              <span class="upload-modal__name" :title="task.fileName">{{ task.fileName }}</span>
              <a-tag :color="taskTagColor(task.status)">{{ taskTagText(task.status) }}</a-tag>
              <AppButton
                v-if="task.status === 'preview_failed'"
                variant="link"
                size="sm"
                @click.stop="emit('retryTask', task.id)"
              >
                重试
              </AppButton>
              <AppButton
                variant="text"
                size="sm"
                class="upload-modal__remove"
                :disabled="task.status === 'uploading'"
                @click.stop="emit('removeTask', task.id)"
              >
                <DeleteOutlined />
              </AppButton>
            </div>
          </template>
          <a-progress v-if="task.status === 'previewing'" :percent="task.progress" size="small" />
          <a-alert
            v-else-if="task.status === 'preview_failed'"
            type="error"
            :message="task.error"
            show-icon
          />
          <StandardMetadataForm
            v-else
            :model-value="task.form"
            @update:model-value="(value) => emit('updateForm', task.id, value)"
          />
        </a-collapse-panel>
      </a-collapse>

      <p v-else class="upload-modal__empty">请先选择 PDF 文件</p>
    </div>

    <div class="upload-modal__footer">
      <span class="upload-modal__tip">AI 预读后请核对元数据，名称和编号必填</span>
      <a-space :size="8">
        <AppButton @click="emit('update:open', false)">取消</AppButton>
        <AppButton variant="primary" :disabled="readyCount === 0" @click="emit('submit')">
          上传（{{ readyCount }}）
        </AppButton>
      </a-space>
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { AppButton } from '@shared/web'
import { computed, ref } from 'vue'
import { DeleteOutlined, FilePdfOutlined, InboxOutlined } from '@ant-design/icons-vue'
import StandardMetadataForm from './StandardMetadataForm.vue'
import type { StandardUploadTask, UploadTaskStatus } from '../composables/useStandardUpload'
import type { StandardPropertyInput } from '@/types'

const props = defineProps<{
  open: boolean
  tasks: StandardUploadTask[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'addFiles': [files: File[]]
  'updateForm': [id: string, form: StandardPropertyInput]
  'removeTask': [id: string]
  'retryTask': [id: string]
  'submit': []
}>()

const activeKey = ref<string[]>([])

const readyCount = computed(() => props.tasks.filter((t) => t.status === 'ready').length)

function handleBeforeUpload(file: File): boolean {
  emit('addFiles', [file])
  return false
}

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

.upload-modal {
  &__list {
    margin-top: @spacing-md;
  }

  &__empty {
    padding: @spacing-xl 0;
    text-align: center;
    color: @text-tertiary;
  }

  &__row {
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
  }

  &__remove {
    flex-shrink: 0;
  }

  &__footer {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: @spacing-md;
    margin-top: @spacing-md;
    padding-top: @spacing-md;
    border-top: 1px solid @border-color;
  }

  &__tip {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }
}

@media (prefers-reduced-motion: reduce) {
  .upload-modal :deep(.ant-collapse-content) {
    transition: none;
  }
}
</style>
