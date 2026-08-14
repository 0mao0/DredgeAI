<template>
  <div class="upload-page">
    <div class="upload-page__intro">
      <div class="upload-page__intro-text">
        <h2 class="upload-page__title">比标分析</h2>
        <p class="upload-page__desc">上传 2~5 份投标文件，系统自动解析、两两比对并生成风险发现</p>
      </div>
      <div class="upload-page__intro-actions">
        <div class="upload-page__name">
          <span class="upload-page__label">项目名称</span>
          <a-input
            :value="name"
            :maxlength="20"
            show-count
            class="upload-page__name-input"
            placeholder="AI 将根据上传文件自动命名，可修改"
            @change="onNameChange"
          />
        </div>
        <a-dropdown trigger="click" placement="bottomRight">
          <a-button size="small">
            <HistoryOutlined />历史任务
          </a-button>
          <template #overlay>
            <a-menu class="upload-page__history" @click="onHistoryClick">
              <a-menu-item v-for="t in history" :key="t.id">
                <span class="upload-page__history-name" :title="t.name">{{ t.name }}</span>
                <a-tag :color="COMPARE_STATUS_MAP[t.status].color">{{ COMPARE_STATUS_MAP[t.status].text }}</a-tag>
              </a-menu-item>
              <a-menu-item v-if="!history.length" key="empty" disabled>暂无历史任务</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
      </div>
    </div>

    <div class="upload-page__main">
      <div class="upload-card">
        <div class="upload-card__head">
          <div class="upload-card__titles">
            <span class="upload-card__title">投标文件</span>
            <span class="upload-card__count">{{ bidCount }}/5</span>
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
            <p class="upload-drop__hint">已选 {{ bidCount }} 份，可继续添加至 5 份</p>
          </div>
        </a-upload-dragger>
      </div>

      <div class="upload-card upload-card--tender">
        <div class="upload-card__head">
          <div class="upload-card__titles">
            <span class="upload-card__title">招标文件</span>
          </div>
          <span class="upload-card__hint">可选 · 用于提取招标要求与响应核对</span>
        </div>
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
      </div>

      <div v-if="items.length" class="upload-list">
        <div
          v-for="item in items"
          :key="item.key"
          class="upload-row"
          :class="{ 'upload-row--error': item.status === 'error' }"
        >
          <FilePdfOutlined v-if="!isWord(item.name)" class="upload-row__icon" />
          <FileWordOutlined v-else class="upload-row__icon upload-row__icon--word" />
          <span class="upload-row__name" :title="item.name">{{ item.name }}</span>
          <a-tag v-if="item.role === 'tender'" class="upload-row__tag">招标</a-tag>
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

          <span v-if="item.error" class="upload-row__error" :title="item.error">{{ item.error }}</span>
          <a-button
            v-if="item.status === 'error'"
            type="link"
            size="small"
            class="upload-row__retry"
            :disabled="creating"
            @click="emit('retry', item.key)"
          >
            重试
          </a-button>
          <a-button
            type="text"
            size="small"
            class="upload-row__remove"
            :disabled="creating || item.status === 'uploading'"
            @click="emit('remove', item.key)"
          >
            <DeleteOutlined />
          </a-button>
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

        <a-button type="primary" size="large" :loading="creating" :disabled="bidCount < 2" @click="emit('start', name)">
          开始分析
        </a-button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import {
  CheckCircleFilled,
  CloseCircleFilled,
  DeleteOutlined,
  ExclamationCircleOutlined,
  FilePdfOutlined,
  FileWordOutlined,
  HistoryOutlined,
  InboxOutlined,
  PaperClipOutlined,
} from '@ant-design/icons-vue'
import { computed } from 'vue'
import { COMPARE_STATUS_MAP, formatFileSize } from '../constants'
import type { CompareTask } from '@/types'

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
}

const props = defineProps<{
  name: string
  items: UploadFileItem[]
  creating: boolean
  history: CompareTask[]
  uploadError?: string
}>()

const emit = defineEmits<{
  'update:name': [value: string]
  'addFiles': [files: { file: File, role: 'bid' | 'tender' }[]]
  'remove': [key: string]
  'retry': [key: string]
  'start': [name: string]
  'historyOpen': [taskId: string]
}>()

const bidCount = computed(() => props.items.filter((i) => i.role === 'bid' && i.status !== 'error').length)

function isWord(name: string): boolean {
  return /\.docx?$/i.test(name)
}

function onNameChange(e: Event): void {
  emit('update:name', (e.target as HTMLInputElement).value)
}

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

function onHistoryClick({ key }: { key: string }): void {
  if (key !== 'empty') emit('historyOpen', key)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.upload-page {
  height: 100%;
  overflow: auto;
  padding: @page-padding;
  display: flex;
  flex-direction: column;
  gap: @spacing-lg;
}

.upload-page__intro {
  flex-shrink: 0;
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: @spacing-lg;
  flex-wrap: wrap;

  &-text {
    min-width: 280px;
  }

  &-actions {
    display: flex;
    align-items: center;
    gap: @spacing-md;
    flex-wrap: wrap;
  }
}

.upload-page__title {
  margin: 0;
  font-size: 22px;
  line-height: 1.3;
  font-weight: @font-weight-semibold;
  color: @text-primary;
}

.upload-page__desc {
  margin: @spacing-xs 0 0;
  font-size: @font-size-sm;
  color: @text-tertiary;
}

.upload-page__name {
  display: flex;
  align-items: center;
  gap: @spacing-md;
}

.upload-page__label {
  flex-shrink: 0;
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: @text-primary;
  white-space: nowrap;
}

.upload-page__name-input {
  width: 300px;
}

.upload-page__history {
  max-width: 320px;

  &-name {
    display: inline-block;
    max-width: 180px;
    overflow: hidden;
    text-overflow: ellipsis;
    vertical-align: middle;
  }
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
