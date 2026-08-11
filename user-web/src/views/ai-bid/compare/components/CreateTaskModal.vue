<template>
  <a-modal
    :open="open"
    title="新建比标任务"
    width="640px"
    :confirm-loading="creating"
    :ok-button-props="{ disabled: !canCreate }"
    ok-text="开始分析"
    cancel-text="取消"
    destroy-on-close
    @ok="handleCreate"
    @cancel="handleCancel"
  >
    <div class="create-form">
      <!-- 投标文件（主上传区） -->
      <div class="create-field">
        <div class="create-field__label">
          投标文件
          <span class="create-field__count">{{ docs.length }}/5</span>
        </div>

        <div v-if="docs.length" class="doc-list">
          <div v-for="(d, i) in docs" :key="i" class="doc-row">
            <FilePdfOutlined class="doc-row__icon" />
            <span class="doc-row__name" :title="d.name">{{ d.name }}</span>
            <span class="doc-row__size">{{ formatSize(d.size) }}</span>
            <a-button type="text" size="small" @click="docs.splice(i, 1)">
              <DeleteOutlined />
            </a-button>
          </div>
        </div>

        <a-upload-dragger
          :multiple="true"
          accept=".pdf"
          :show-upload-list="false"
          :before-upload="handleUpload"
        >
          <p class="ant-upload-drag-icon"><InboxOutlined /></p>
          <p class="ant-upload-text">点击或拖拽上传投标文件</p>
          <p class="ant-upload-hint">仅支持 PDF，2~5 份，单份不超过 100MB</p>
        </a-upload-dragger>
      </div>

      <!-- 招标文件（可选，次级附件位） -->
      <div class="create-tender">
        <div v-if="tenderDoc" class="doc-row">
          <FilePdfOutlined class="doc-row__icon" />
          <span class="doc-row__name" :title="tenderDoc.name">{{ tenderDoc.name }}</span>
          <a-tag class="doc-row__tag">招标文件</a-tag>
          <span class="doc-row__size">{{ formatSize(tenderDoc.size) }}</span>
          <a-button type="text" size="small" @click="tenderDoc = null">
            <DeleteOutlined />
          </a-button>
        </div>
        <a-upload
          v-else
          accept=".pdf"
          :show-upload-list="false"
          :before-upload="handleTenderUpload"
          class="create-tender__upload"
        >
          <div class="tender-strip">
            <PaperClipOutlined class="tender-strip__icon" />
            <span class="tender-strip__text">添加招标文件</span>
            <span class="tender-strip__hint">可选 · 上传后用于提取招标要求与响应核对</span>
          </div>
        </a-upload>
      </div>
    </div>

    <!-- 项目名称：AI 自动命名 -->
    <div class="create-name">
      <span class="create-name__label">项目名称</span>
      <a-input
        v-model:value="name"
        :maxlength="10"
        show-count
        placeholder="AI 将根据上传文件自动命名，可修改"
        @input="userEdited = true"
      />
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import {
  InboxOutlined,
  FilePdfOutlined,
  DeleteOutlined,
  PaperClipOutlined,
} from '@ant-design/icons-vue'

interface PickedFile {
  name: string
  size: number
  file: File
}

defineProps<{ open: boolean }>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'created': [payload: { name: string, files: File[], tenderFile?: File }]
}>()

const name = ref('')
const userEdited = ref(false)
const docs = ref<PickedFile[]>([])
const tenderDoc = ref<PickedFile | null>(null)
const creating = ref(false)

// 份数够即可开始，不必等上传完成（点击后自动等收尾）
const canCreate = computed(() => docs.value.length >= 2 && docs.value.length <= 5)

function formatSize(size: number): string {
  if (size >= 1024 * 1024) return `${(size / 1024 / 1024).toFixed(1)} MB`
  if (size >= 1024) return `${(size / 1024).toFixed(0)} KB`
  return `${size} B`
}

/** AI 自动命名：取各文件名的公共前缀，去除「投标文件」等后缀，不超过 10 字 */
function deriveName(): string {
  const names = docs.value.map((d) => d.name)
  if (!names.length) return ''
  const stems = names.map((n) => n.replace(/\.[^.]+$/, ''))
  let prefix = stems[0]
  for (const s of stems.slice(1)) {
    while (prefix && !s.startsWith(prefix)) prefix = prefix.slice(0, -1)
  }
  const cleaned = prefix.replace(/(投标文件|报价文件|标书)+[\s_（(]?[A-E甲丁乙丙]?[)）]?$/g, '').trim()
  const base = cleaned.length >= 2 ? cleaned : stems[0]
  return base.slice(0, 10)
}

watch([docs, tenderDoc], () => {
  if (!userEdited.value) name.value = deriveName()
}, { deep: true })

function handleUpload(file: File): boolean {
  if (docs.value.length >= 5) {
    return false
  }
  docs.value.push({ name: file.name, size: file.size, file })
  return false
}

function handleTenderUpload(file: File): boolean {
  tenderDoc.value = { name: file.name, size: file.size, file }
  return false
}

function reset(): void {
  name.value = ''
  userEdited.value = false
  docs.value = []
  tenderDoc.value = null
  creating.value = false
}

function handleCancel(): void {
  emit('update:open', false)
  reset()
}

async function handleCreate(): Promise<void> {
  if (!canCreate.value || creating.value) return
  creating.value = true
  emit('created', {
    name: name.value.trim() || deriveName() || '比标任务',
    files: docs.value.map((d) => d.file),
    tenderFile: tenderDoc.value?.file,
  })
  emit('update:open', false)
  reset()
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.create-form {
  display: flex;
  flex-direction: column;
  gap: @spacing-base;
}

.create-field {
  min-width: 0;

  &__label {
    font-size: @font-size-sm;
    font-weight: @font-weight-medium;
    color: @text-primary;
    margin-bottom: @spacing-sm;
    display: flex;
    align-items: center;
  }

  &__count {
    margin-left: @spacing-xs;
    font-size: @font-size-xs;
    color: @text-tertiary;
    font-weight: @font-weight-regular;
  }

  // 拖拽框撑满整行
  :deep(.ant-upload-wrapper),
  :deep(.ant-upload-drag) {
    width: 100%;
  }
}

.create-tender {
  &__upload {
    display: block;
  }
}

.tender-strip {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-sm @spacing-md;
  border: 1px dashed @border-color;
  border-radius: @radius-base;
  background: @content-bg;
  cursor: pointer;
  transition: border-color @transition-fast;

  &:hover {
    border-color: @brand-primary;
  }

  &__icon {
    color: @text-tertiary;
  }

  &__text {
    font-size: @font-size-sm;
    color: @text-secondary;
  }

  &__hint {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }
}

.doc-list {
  display: flex;
  flex-direction: column;
  gap: @spacing-xs;
  margin-bottom: @spacing-sm;
}

.doc-row {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: 6px @spacing-sm;
  background: @content-bg;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  font-size: @font-size-sm;

  &__icon { color: @danger; flex-shrink: 0; }
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
}

.create-name {
  margin-top: @spacing-lg;
  padding-top: @spacing-base;
  border-top: 1px solid @divider-color;
  display: flex;
  align-items: center;
  gap: @spacing-md;

  &__label {
    flex-shrink: 0;
    font-size: @font-size-sm;
    font-weight: @font-weight-medium;
    color: @text-primary;
    white-space: nowrap;
  }
}
</style>
