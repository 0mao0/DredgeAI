<template>
  <div class="upload-page">
    <div class="upload-page__main">
      <div class="upload-card">
        <div class="upload-card__head">
          <div class="upload-card__titles">
            <span class="upload-card__title">投标文件</span>
            <span class="upload-card__count">{{ bidCount }}/{{ MAX_BID_DOCUMENTS }}</span>
          </div>
          <span class="upload-card__hint">支持 PDF / Word，单份不超过 100MB</span>
        </div>
        <a-upload-dragger
          multiple
          accept=".pdf,.doc,.docx"
          :show-upload-list="false"
          :before-upload="onPickBid"
        >
          <div class="upload-drop">
            <InboxOutlined class="upload-drop__icon" />
            <p class="upload-drop__text">点击或拖拽上传投标文件</p>
            <p class="upload-drop__hint">已选 {{ bidCount }} 份，可继续添加至 {{ MAX_BID_DOCUMENTS }} 份</p>
          </div>
        </a-upload-dragger>

        <div v-if="bidItems.length" class="upload-list">
          <UploadFileRow
            v-for="item in bidItems"
            :key="item.key"
            :item="item"
            :disabled="creating"
            @retry="emit('retry', item.key)"
            @remove="emit('remove', item.key)"
          />
        </div>
      </div>

      <div class="upload-card upload-card--tender">
        <div
          class="upload-card__head upload-card__head--tender"
          @click="tenderExpanded = !tenderExpanded"
        >
          <div class="upload-card__titles">
            <DownOutlined v-if="tenderExpanded" class="upload-card__caret" />
            <RightOutlined v-else class="upload-card__caret" />
            <span class="upload-card__title">招标文件</span>
            <span class="upload-card__optional">可选</span>
          </div>
          <span v-if="tenderExpanded" class="upload-card__hint">用于提取招标要求与响应核对</span>
        </div>
        <div v-if="tenderExpanded" class="upload-card__tender-body">
          <a-upload
            accept=".pdf,.doc,.docx"
            :show-upload-list="false"
            :before-upload="onPickTender"
          >
            <div class="tender-strip">
              <PaperClipOutlined class="tender-strip__icon" />
              <span class="tender-strip__text">添加招标文件</span>
            </div>
          </a-upload>

          <div v-if="tenderItems.length" class="upload-list">
            <UploadFileRow
              v-for="item in tenderItems"
              :key="item.key"
              :item="item"
              :disabled="creating"
              @retry="emit('retry', item.key)"
              @remove="emit('remove', item.key)"
            />
          </div>
        </div>
      </div>

      <div class="upload-page__footer">
        <span v-if="bidCount < 2" class="upload-page__hint">
          <ExclamationCircleOutlined />至少需要 2 份投标文件（当前 {{ bidCount }} 份）
        </span>
        <span v-else-if="uploadError" class="upload-page__hint upload-page__hint--error">
          <ExclamationCircleOutlined />{{ uploadError }}
        </span>
        <span v-else class="upload-page__hint">上传失败可在行内重试，全部失败不会进入工作区</span>

        <a-button type="primary" size="large" :loading="creating" :disabled="bidCount < 2" @click="emit('start')">
          开始分析
        </a-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {
  ExclamationCircleOutlined,
  DownOutlined,
  InboxOutlined,
  PaperClipOutlined,
  RightOutlined,
} from '@ant-design/icons-vue'
import { computed, ref } from 'vue'
import { MAX_BID_DOCUMENTS } from '../constants'
import UploadFileRow from './UploadFileRow.vue'

export interface UploadFileItem {
  key: string
  name: string
  size: number
  file: File
  role: 'bid' | 'tender'
  status: 'pending' | 'uploading' | 'done' | 'error'
  error?: string
  docId?: string
  percent?: number
  startedAt?: number
}

const props = defineProps<{
  items: UploadFileItem[]
  creating: boolean

  uploadError?: string
}>()

const emit = defineEmits<{
  addFiles: [files: { file: File, role: 'bid' | 'tender' }[]]
  remove: [key: string]
  retry: [key: string]
  start: []
}>()

const tenderExpanded = ref(false)

const bidCount = computed(() => props.items.filter((i) => i.role === 'bid' && i.status !== 'error').length)
const bidItems = computed(() => props.items.filter((i) => i.role === 'bid'))
const tenderItems = computed(() => props.items.filter((i) => i.role === 'tender'))

function onPick(file: File, role: 'bid' | 'tender'): boolean {
  emit('addFiles', [{ file, role }])
  return false // 上传由 index.vue 统一调度（创建任务后逐份提交）
}

function onPickBid(file: File): boolean {
  return onPick(file, 'bid')
}

function onPickTender(file: File): boolean {
  return onPick(file, 'tender')
}

/* function onHistoryClick({ key }: { key: string }): void {
  if (key !== 'empty') emit('historyOpen', key)
}
*/
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.upload-page {
  height: 100%;
  overflow: auto;
  display: flex;
  flex-direction: column;
  gap: @spacing-lg;
}

.upload-page__main {
  flex-shrink: 0;
  max-width: 820px;
  margin: 0 auto;
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: @spacing-base;
}

.upload-card {
  flex-shrink: 0;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  padding: @spacing-base @spacing-lg @spacing-lg;

  &__head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: @spacing-md;
    margin-bottom: @spacing-md;
  }

  &__titles {
    display: flex;
    align-items: baseline;
    gap: @spacing-sm;
  }

  &__title {
    font-size: @font-size-sm;
    font-weight: @font-weight-medium;
    color: @text-primary;
  }

  &__count {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }

  &__hint {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }

  &__caret {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }

  &__optional {
    display: inline-flex;
    align-items: center;
    height: 18px;
    padding: 0 6px;
    border-radius: @radius-sm;
    background: color-mix(in srgb, @text-tertiary 12%, transparent);
    font-size: @font-size-xs;
    color: @text-secondary;
    line-height: 1;
  }

  &__head--tender {
    margin-bottom: 0;
    cursor: pointer;
    user-select: none;
  }

  &__tender-body {
    margin-top: @spacing-md;
  }

  :deep(.ant-upload-wrapper) {
    display: block;
  }

  :deep(.ant-upload) {
    display: block;
  }

  :deep(.ant-upload-drag) {
    background: color-mix(in srgb, @brand-primary 4%, @card-bg);
    border-color: @border-color;
    border-radius: @radius-base;
    transition: border-color @transition-fast;
  }

  :deep(.ant-upload-drag:hover) {
    border-color: @brand-primary;
  }

  :deep(.ant-upload-btn) {
    padding: @spacing-xl @spacing-base;
  }

  &--tender {
    :deep(.ant-upload-btn) {
      padding: 0;
    }
  }
}

.upload-drop {
  &__icon {
    font-size: 40px;
    color: @brand-primary;
  }

  &__text {
    margin: @spacing-sm 0 0;
    font-size: @font-size-base;
    color: @text-primary;
  }

  &__hint {
    margin: @spacing-xs 0 0;
    font-size: @font-size-xs;
    color: @text-tertiary;
  }
}

.tender-strip {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: @spacing-sm;
  width: 100%;
  padding: @spacing-md;
  border: 1px dashed @border-color;
  border-radius: @radius-base;
  background: color-mix(in srgb, @brand-primary 3%, @card-bg);
  cursor: pointer;
  transition: border-color @transition-fast, background-color @transition-fast;

  &:hover {
    border-color: @brand-primary;
    background: color-mix(in srgb, @brand-primary 6%, @card-bg);
  }

  &__icon {
    color: @text-tertiary;
  }

  &__text {
    font-size: @font-size-sm;
    color: @text-secondary;
  }
}

.upload-list {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: @spacing-xs;
}

.upload-page__footer {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: @spacing-md;
  padding: @spacing-sm 0 0;
}

.upload-page__hint {
  font-size: @font-size-xs;
  color: @text-tertiary;

  &--error {
    color: @danger;
  }
}
</style>
