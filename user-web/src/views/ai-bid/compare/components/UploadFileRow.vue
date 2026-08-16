<template>
  <div class="upload-row" :class="{ 'upload-row--error': item.status === 'error' }">
    <FilePdfOutlined v-if="!isWord(item.name)" class="upload-row__icon" />
    <FileWordOutlined v-else class="upload-row__icon upload-row__icon--word" />
    <a-tag v-if="item.role === 'tender'" class="upload-row__tag">招标</a-tag>
    <span class="upload-row__name" :title="item.name">{{ item.name }}</span>
    <span class="upload-row__size">{{ formatFileSize(item.size) }}</span>

    <div v-if="item.status === 'uploading'" class="upload-row__progress">
      <a-progress
        :percent="item.percent ?? 0"
        :show-info="false"
        size="small"
        class="upload-row__bar"
      />
      <span class="upload-row__percent">{{ item.percent ?? 0 }}%</span>
    </div>
    <CheckCircleFilled v-else-if="item.status === 'done'" class="upload-row__ok" />
    <CloseCircleFilled v-else-if="item.status === 'error'" class="upload-row__bad" />
    <span v-else class="upload-row__pending">等待上传</span>

    <span v-if="item.error" class="upload-row__error" :title="item.error">{{ item.error }}</span>
    <a-button
      v-if="item.status === 'error'"
      type="link"
      size="small"
      class="upload-row__retry"
      :disabled="disabled"
      @click="emit('retry')"
    >
      重试
    </a-button>
    <a-button
      type="text"
      size="small"
      class="upload-row__remove"
      :disabled="disabled || item.status === 'uploading'"
      @click="emit('remove')"
    >
      <DeleteOutlined />
    </a-button>
  </div>
</template>

<script setup lang="ts">
import {
  CheckCircleFilled,
  CloseCircleFilled,
  DeleteOutlined,
  FilePdfOutlined,
  FileWordOutlined,
} from '@ant-design/icons-vue'
import { formatFileSize } from '../constants'
import type { UploadFileItem } from './UploadPage.vue'

defineProps<{
  item: UploadFileItem
  disabled?: boolean
}>()

const emit = defineEmits<{
  retry: []
  remove: []
}>()

function isWord(name: string): boolean {
  return /\.docx?$/i.test(name)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.upload-row {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: 8px @spacing-sm;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  font-size: @font-size-sm;

  &--error {
    border-color: @danger;
    background: color-mix(in srgb, @danger 6%, @card-bg);
  }

  &__icon { color: @danger; flex-shrink: 0; }
  &__icon--word { color: @brand-primary; }

  &__name {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    color: @text-primary;
  }

  &__tag { flex-shrink: 0; }
  &__size { flex-shrink: 0; font-size: @font-size-xs; color: @text-tertiary; }
  &__pending { flex-shrink: 0; font-size: @font-size-xs; color: @text-tertiary; }

  &__progress {
    flex: 0 1 220px;
    min-width: 140px;
    display: flex;
    align-items: center;
    gap: @spacing-sm;
  }

  &__bar {
    flex: 1;
    min-width: 0;
    margin: 0;
  }

  &__percent {
    flex-shrink: 0;
    width: 38px;
    text-align: right;
    font-size: @font-size-xs;
    color: @text-secondary;
    font-variant-numeric: tabular-nums;
  }

  &__ok { color: @success; flex-shrink: 0; }
  &__bad { color: @danger; flex-shrink: 0; }

  &__error {
    max-width: 220px;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
    font-size: @font-size-xs;
    color: @danger;
  }

  &__retry { flex-shrink: 0; }
  &__remove { flex-shrink: 0; }
}
</style>
