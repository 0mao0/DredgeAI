<template>
  <div class="read-page">
    <!-- 上传引导页 -->
    <div v-if="view === 'upload'" class="read-upload">
      <div class="read-upload__main">
        <div class="upload-card">
          <div class="upload-card__head">
            <div class="upload-card__titles">
              <span class="upload-card__title">招标文件</span>
              <span class="upload-card__count">{{ uploadItem ? 1 : 0 }}/1</span>
            </div>
            <span class="upload-card__hint">支持 PDF / Word，单份不超过 100MB</span>
          </div>

          <a-upload-dragger
            accept=".pdf,.doc,.docx"
            :show-upload-list="false"
            :before-upload="onPickFile"
            :disabled="creating || starting"
          >
            <div class="upload-drop">
              <InboxOutlined class="upload-drop__icon" />
              <p class="upload-drop__text">点击或拖拽上传招标文件</p>
              <p class="upload-drop__hint">上传后自动创建读标任务，解析完成后提取项目名称</p>
            </div>
          </a-upload-dragger>

          <div v-if="uploadItem" class="upload-list">
            <UploadFileRow
              :item="uploadItem"
              :disabled="creating || starting"
              @retry="retryUpload"
              @remove="removeUpload"
            />
          </div>
        </div>

        <div class="read-upload__footer">
          <span v-if="uploadError" class="read-upload__hint read-upload__hint--error">
            <ExclamationCircleOutlined />{{ uploadError }}
          </span>
          <span v-else class="read-upload__hint">
            上传完成后再点击「开始读标」，系统将自动解析并抽取项目名称
          </span>
          <AppButton
            variant="primary"
            size="lg"
            :loading="starting"
            :disabled="!canStart"
            @click="startRead"
          >
            开始读标
          </AppButton>
        </div>
      </div>
    </div>

    <!-- 读标工作区 -->
    <div v-else-if="task" class="read-workspace">
      <div class="read-workspace__bar">
        <div class="read-workspace__name">
          <span class="read-workspace__name-title">{{ task.name }}</span>
          <a-tag :color="statusColor(task.status)">{{ statusText(task.status) }}</a-tag>
          <a-tag>基准库 v{{ task.baselineVersion }}</a-tag>
        </div>
        <AppButton size="sm" @click="backToUpload">
          <PlusOutlined />
          新建读标
        </AppButton>
        <AppButton size="sm" variant="primary" :loading="exporting" @click="exportBaseline">
          <DownloadOutlined />
          导出 JSON
        </AppButton>
      </div>

      <div v-if="task.failureReason" class="read-workspace__banner">
        <WarningOutlined />{{ task.failureReason }}
        <AppButton
          v-if="task.status === 'failed'"
          size="sm"
          :loading="reparsing"
          @click="onReparseTask"
        >
          重新解析
        </AppButton>
      </div>

      <div v-if="isPollingStatus(task.status)" class="read-workspace__progress">
        <span class="read-workspace__progress-label">{{ statusText(task.status) }}</span>
        <a-progress
          :percent="task.progressPercent"
          size="small"
          class="read-workspace__progress-bar"
        />
        <span v-if="documents[0]?.parseStageMessage" class="read-workspace__progress-message">
          {{ documents[0].parseStageMessage }}
        </span>
      </div>

      <div class="read-workspace__split">
        <!-- 左：目录 -->
        <SectionCard title="目录" class="read-workspace__outline">
          <DataSkeleton v-if="outlineLoading" :rows="6" />
          <a-empty v-else-if="outlineTreeData.length === 0" description="暂无目录" />
          <a-tree
            v-else
            :tree-data="outlineTreeData"
            :default-expand-all="true"
            :selectable="false"
          >
            <template #title="{ title }">
              <span class="read-outline__title">{{ title }}</span>
            </template>
          </a-tree>
        </SectionCard>

        <!-- 中：PDF 预览 -->
        <SectionCard title="PDF 预览" class="read-workspace__pdf">
          <PdfViewer
            v-if="documentFileUrl"
            :file-url="documentFileUrl"
            :title="documentTitle"
            :page="pdfPage"
            :high="pdfHighlights"
            :active-highlight-id="selectedFieldId || undefined"
            hide-original-label
            @update:page="pdfPage = $event"
            @select-highlight="onPdfHighlightSelect"
          />
          <a-empty v-else description="请先上传并解析招标文件" />
        </SectionCard>

        <!-- 右：基准库 -->
        <SectionCard title="基准库" class="read-workspace__baseline">
          <template #extra>
            <a-popconfirm
              title="重新抽取将覆盖该类别的现有字段（含已确认内容），确定继续吗？"
              ok-text="确定"
              cancel-text="取消"
              @confirm="reExtractCategory"
            >
              <AppButton size="sm" :loading="reExtracting">
                <ReloadOutlined />
                重抽本类
              </AppButton>
            </a-popconfirm>
          </template>
          <a-tabs v-model:active-key="activeCategory" size="small" class="read-workspace__tabs">
            <a-tab-pane
              v-for="cat in baselineCategoryOptions"
              :key="cat.value"
              :tab="cat.label"
            />
          </a-tabs>

          <div v-if="baselineLoading" class="read-workspace__baseline-body">
            <DataSkeleton :rows="4" />
          </div>
          <a-empty v-else-if="activeFields.length === 0" description="该分类暂无字段" />
          <div v-else class="read-workspace__baseline-body">
            <div
              v-for="field in activeFields"
              :key="field.id"
              class="baseline-field"
              :class="{ 'baseline-field--active': selectedFieldId === field.id }"
              @click="locateField(field)"
            >
              <div class="baseline-field__head">
                <span class="baseline-field__key">{{ field.fieldKey }}</span>
                <div class="baseline-field__actions">
                  <a-tag :color="fieldStatusColor(field.status)">{{ fieldStatusText(field.status) }}</a-tag>
                  <AppButton
                    v-if="field.status === 'auto' || field.status === 'needs_review'"
                    variant="link"
                    size="sm"
                    :loading="confirmingFieldId === field.id"
                    @click.stop="confirmField(field)"
                  >
                    确认
                  </AppButton>
                </div>
              </div>
              <div class="baseline-field__value">{{ formatFieldValue(field.valueJson) }}</div>
              <div class="baseline-field__meta">
                <span>置信度 {{ Math.round(field.confidence * 100) }}%</span>
                <span>{{ field.extractor }} · {{ field.extractorVersion }}</span>
              </div>
              <div v-if="field.sourceRefs.length" class="baseline-field__refs">
                <span
                  v-for="(source, i) in field.sourceRefs"
                  :key="`${source.fieldId}-${i}`"
                  class="baseline-field__ref"
                >
                  第 {{ source.pageIdx + 1 }} 页 · {{ source.blockId }}
                </span>
              </div>
            </div>
          </div>
        </SectionCard>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { message } from 'ant-design-vue'
import {
  DownloadOutlined,
  ExclamationCircleOutlined,
  InboxOutlined,
  PlusOutlined,
  ReloadOutlined,
  WarningOutlined,
} from '@ant-design/icons-vue'
import { AppButton, DataSkeleton, SectionCard } from '@shared/web'
import type { BaselineCategory, BaselineField, BlockRange, SourceRef, TenderReadingDocument, TenderReadingOutlineNode, TenderReadingTask } from '@/types'
import UploadFileRow from '../compare/components/UploadFileRow.vue'
import type { UploadFileItem } from '../compare/components/UploadPage.vue'
import PdfViewer from '../compare/components/PdfViewer.vue'
import {
  createTenderReadTask,
  deleteTenderReadTask,
  exportTenderReadBaseline,
  getTenderReadBaseline,
  getTenderReadDocumentFileUrl,
  getTenderReadDocuments,
  getTenderReadOutline,
  getTenderReadTask,
  reExtractTenderReadBaseline,
  reparseTenderReadTask,
  startTenderReadParse,
  updateTenderReadField,
  uploadTenderReadDocument,
} from '@/api/modules/tenderRead'

interface TreeData {
  key: string
  title: string
  children: TreeData[]
}

const view = ref<'upload' | 'workspace'>('upload')
const uploadItem = ref<UploadFileItem | null>(null)
const currentTaskId = ref('')
const uploadError = ref('')
const creating = ref(false)
const starting = ref(false)
const task = ref<TenderReadingTask | null>(null)
const documents = ref<TenderReadingDocument[]>([])
const outline = ref<TenderReadingOutlineNode[]>([])
const baseline = ref<BaselineField[]>([])
const outlineLoading = ref(false)
const baselineLoading = ref(false)
const exporting = ref(false)
const pdfPage = ref(1)
const pdfHighlights = ref<BlockRange[]>([])
const selectedFieldId = ref('')
const activeCategory = ref<BaselineCategory>('project_info')
const reExtracting = ref(false)
const confirmingFieldId = ref('')
const reparsing = ref(false)
let pollTimer: number | null = null
let pollGen = 0
let pollInFlight = false
let disposed = false

const POLL_MS = 3000

const canStart = computed(() => uploadItem.value?.status === 'done' && !!currentTaskId.value)
const outlineTreeData = computed<TreeData[]>(() => outline.value.map(toTreeData))
const documentFileUrl = computed(() => task.value && documents.value.length > 0 ? getTenderReadDocumentFileUrl(task.value.id) : '')
const documentTitle = computed(() => documents.value[0]?.fileName ?? '')
const activeFields = computed(() => baseline.value.filter((f) => f.category === activeCategory.value))

const baselineCategoryOptions: { value: BaselineCategory, label: string }[] = [
  { value: 'project_info', label: '项目信息' },
  { value: 'rejection_clauses', label: '废标条款' },
  { value: 'evaluation_criteria', label: '评分标准' },
  { value: 'technical_parameters', label: '技术参数' },
  { value: 'commercial_data', label: '商务数据' },
  { value: 'chapter_outline', label: '章节框架' },
  { value: 'seal_rules', label: '签章规则' },
  { value: 'dark_bid_format_rules', label: '暗标格式' },
]

watch(() => task.value?.status, (status) => {
  if (!status) return
  if (isPollingStatus(status)) {
    startPolling()
  } else {
    stopPolling()
    // 后台重抽到达终态：复位按钮并刷新高亮（基准库已由最后一轮轮询刷新）
    if (reExtracting.value) {
      reExtracting.value = false
      autoHighlightFirstField()
    }
  }
})

onBeforeUnmount(() => {
  disposed = true
  stopPolling()
})

async function onPickFile(file: File): Promise<boolean> {
  if (creating.value || starting.value) return false
  if (!/\.(?:pdf|doc|docx)$/i.test(file.name)) {
    uploadError.value = '仅支持 PDF / Word 文档'
    return false
  }
  if (file.size > 100 * 1024 * 1024) {
    uploadError.value = '单份文件不能超过 100MB'
    return false
  }

  uploadError.value = ''
  const key = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
  uploadItem.value = {
    key,
    name: file.name,
    size: file.size,
    file,
    role: 'tender',
    status: 'uploading',
    percent: 0,
  }
  await createAndUpload(key, file)
  return false
}

async function createAndUpload(key: string, file: File): Promise<void> {
  creating.value = true
  try {
    if (!currentTaskId.value) {
      const t = await createTenderReadTask({ name: '读标任务' })
      currentTaskId.value = t.id
    }

    await uploadTenderReadDocument(currentTaskId.value, file, (p) => {
      setUploadPercent(key, p)
    })

    setUploadItem(key, { status: 'done', percent: 100 })
    uploadError.value = ''
    // 上传完成后自动创建解析任务并进入工作区
    await startRead()
  } catch (error) {
    setUploadItem(key, { status: 'error', error: error instanceof Error ? error.message : '上传失败' })
  } finally {
    creating.value = false
  }
}

function setUploadPercent(key: string, percent: number): void {
  setUploadItem(key, { percent })
}

function setUploadItem(key: string, patch: Partial<UploadFileItem>): void {
  if (uploadItem.value?.key !== key) return
  uploadItem.value = { ...uploadItem.value, ...patch }
}

async function retryUpload(): Promise<void> {
  const item = uploadItem.value
  if (!item || creating.value) return
  setUploadItem(item.key, { status: 'uploading', error: undefined, percent: 0 })
  await createAndUpload(item.key, item.file)
}

async function removeUpload(): Promise<void> {
  if (currentTaskId.value) {
    try {
      await deleteTenderReadTask(currentTaskId.value)
    } catch {
      // 删除失败不阻塞本地清空
    }
  }
  currentTaskId.value = ''
  uploadItem.value = null
  uploadError.value = ''
}

async function startRead(): Promise<void> {
  if (!currentTaskId.value || starting.value) return
  starting.value = true
  try {
    const t = await startTenderReadParse(currentTaskId.value)
    task.value = t
    view.value = 'workspace'
    uploadItem.value = null
    await loadDetail(t.id)
  } catch (error) {
    message.error(error instanceof Error ? error.message : '开始解析失败')
  } finally {
    starting.value = false
  }
}

function backToUpload(): void {
  stopPolling()
  view.value = 'upload'
  task.value = null
  currentTaskId.value = ''
  uploadItem.value = null
  documents.value = []
  outline.value = []
  baseline.value = []
  pdfHighlights.value = []
  selectedFieldId.value = ''
}

async function loadDetail(id: string): Promise<void> {
  outlineLoading.value = true
  baselineLoading.value = true
  pdfHighlights.value = []
  selectedFieldId.value = ''
  try {
    const [taskDto, docList] = await Promise.all([
      getTenderReadTask(id, true),
      getTenderReadDocuments(id, true),
    ])
    task.value = taskDto
    documents.value = docList

    try {
      outline.value = await getTenderReadOutline(id, true)
    } catch {
      outline.value = []
    }

    try {
      const baselineDto = await getTenderReadBaseline(id, true)
      baseline.value = baselineDto.fields
      autoHighlightFirstField()
    } catch {
      baseline.value = []
    }
  } catch (error) {
    documents.value = []
    outline.value = []
    baseline.value = []
    message.error(error instanceof Error ? error.message : '加载读标详情失败')
  } finally {
    outlineLoading.value = false
    baselineLoading.value = false
  }
}

async function refreshDetail(id: string, gen: number): Promise<void> {
  const stale = (): boolean => disposed || gen !== pollGen || task.value?.id !== id
  try {
    const [taskDto, docList] = await Promise.all([
      getTenderReadTask(id, true),
      getTenderReadDocuments(id, true),
    ])
    if (stale()) return
    task.value = taskDto
    documents.value = docList

    try {
      const outlineData = await getTenderReadOutline(id, true)
      if (stale()) return
      outline.value = outlineData
    } catch {
      // 尚未解析完成时目录不可用，保留当前值
    }

    try {
      const baselineDto = await getTenderReadBaseline(id, true)
      if (stale()) return
      baseline.value = baselineDto.fields
      autoHighlightFirstField()
    } catch {
      // 尚未生成基准库时保持当前值
    }
  } catch (err) {
    if (stale()) return
    if (isNotFound(err)) {
      // 任务在服务端被删除：停止轮询并回退到上传页
      backToUpload()
      uploadError.value = '任务不存在，可能已被删除'
    }
    // 轮询中的单次失败不打断用户操作，下次轮询继续
  }
}

function isNotFound(err: unknown): boolean {
  return (err as { response?: { status?: number } })?.response?.status === 404
}

function startPolling(): void {
  stopPolling()
  schedulePoll(POLL_MS)
}

function schedulePoll(delay: number): void {
  if (disposed) return
  if (pollTimer !== null) window.clearTimeout(pollTimer)
  pollTimer = window.setTimeout(() => {
    pollTimer = null
    void runPoll()
  }, delay)
}

async function runPoll(): Promise<void> {
  const gen = pollGen
  const id = task.value?.id
  if (!id || pollInFlight) return
  pollInFlight = true
  try {
    await refreshDetail(id, gen)
  } finally {
    pollInFlight = false
  }
  if (disposed || gen !== pollGen || task.value?.id !== id) return
  if (task.value && isPollingStatus(task.value.status)) schedulePoll(POLL_MS)
}

function stopPolling(): void {
  pollGen++
  if (pollTimer !== null) {
    window.clearTimeout(pollTimer)
    pollTimer = null
  }
}

function isPollingStatus(status: TenderReadingTask['status']): boolean {
  return ['uploading', 'parsing', 'parsed', 'extracting', 'reviewing'].includes(status)
}

async function exportBaseline(): Promise<void> {
  if (!task.value || exporting.value) return
  exporting.value = true
  try {
    const data = await exportTenderReadBaseline(task.value.id)
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `tender-read-${task.value.id}.json`
    a.click()
    URL.revokeObjectURL(url)
    message.success('导出成功')
  } catch (error) {
    message.error(error instanceof Error ? error.message : '导出失败')
  } finally {
    exporting.value = false
  }
}

function toTreeData(node: TenderReadingOutlineNode): TreeData {
  return {
    key: node.blockId || node.title,
    title: node.title,
    children: node.children.map(toTreeData),
  }
}

function formatFieldValue(valueJson: string): string {
  try {
    const parsed = JSON.parse(valueJson) as unknown
    if (typeof parsed === 'string') return parsed
    return JSON.stringify(parsed)
  } catch {
    return valueJson
  }
}

function locateField(field: BaselineField): void {
  selectedFieldId.value = field.id
  pdfHighlights.value = field.sourceRefs.map(sourceRefToBlockRange)
  const first = field.sourceRefs[0]
  if (first) pdfPage.value = first.pageIdx + 1
}

function autoHighlightFirstField(): void {
  if (selectedFieldId.value || pdfHighlights.value.length > 0) return
  const field = baseline.value.find((f) => f.sourceRefs.length > 0)
  if (field) locateField(field)
}

function onPdfHighlightSelect(highlight: { itemId?: string }): void {
  if (!highlight.itemId) return
  const field = baseline.value.find((f) => f.id === highlight.itemId)
  if (!field) return
  activeCategory.value = field.category
  locateField(field)
}

function sourceRefToBlockRange(ref: SourceRef): BlockRange {
  // bbox 可能缺元素（OCR 低置信块），非法时仅跳页不画框
  const valid = Array.isArray(ref.bbox) && ref.bbox.length === 4
  const [x0, y0, x1, y1] = valid ? ref.bbox : [0, 0, 1, 1]
  return {
    docId: '',
    page: ref.pageIdx + 1,
    bbox: [x0, y0, x1, y1],
    hasRect: valid,
    pairId: ref.fieldId,
    excerpt: ref.text,
  }
}

async function confirmField(field: BaselineField): Promise<void> {
  if (!task.value || confirmingFieldId.value) return
  confirmingFieldId.value = field.id
  try {
    const updated = await updateTenderReadField(task.value.id, field.id, {
      valueJson: field.valueJson,
      rawText: field.rawText,
      status: 'confirmed',
      confidence: field.confidence,
    })
    baseline.value = baseline.value.map((f) => (f.id === updated.id ? updated : f))
    message.success('字段已确认')
  } catch (error) {
    message.error(error instanceof Error ? error.message : '字段确认失败')
  } finally {
    confirmingFieldId.value = ''
  }
}

async function onReparseTask(): Promise<void> {
  if (!task.value || reparsing.value) return
  reparsing.value = true
  try {
    // 状态回到 parsing，status watch 自动起轮询
    task.value = await reparseTenderReadTask(task.value.id)
  } catch (error) {
    message.error(error instanceof Error ? error.message : '重新解析失败')
  } finally {
    reparsing.value = false
  }
}

async function reExtractCategory(): Promise<void> {
  if (!task.value || reExtracting.value) return
  reExtracting.value = true
  try {
    // 重抽为后台任务：返回抽取中的任务快照，状态 watch 自动起轮询，
    // 到达终态后复位按钮并刷新基准库（轮询每 tick 都会重拉基准库）
    task.value = await reExtractTenderReadBaseline(task.value.id, activeCategory.value)
    selectedFieldId.value = ''
    pdfHighlights.value = []
    message.success('已提交重新抽取，完成后自动刷新')
  } catch (error) {
    reExtracting.value = false
    message.error(error instanceof Error ? error.message : '重新抽取提交失败')
  }
}

const statusMap: Record<TenderReadingTask['status'], { text: string, color: string }> = {
  uploading: { text: '上传中', color: 'blue' },
  parsing: { text: '解析中', color: 'blue' },
  parsed: { text: '已解析', color: 'blue' },
  extracting: { text: '抽取中', color: 'blue' },
  reviewing: { text: '待复核', color: 'orange' },
  ready: { text: '已就绪', color: 'green' },
  partial: { text: '部分完成', color: 'orange' },
  failed: { text: '失败', color: 'red' },
}

function statusText(status: TenderReadingTask['status']): string {
  return statusMap[status].text
}

function statusColor(status: TenderReadingTask['status']): string {
  return statusMap[status].color
}

const fieldStatusMap: Record<BaselineField['status'], { text: string, color: string }> = {
  auto: { text: '自动', color: 'blue' },
  needs_review: { text: '待复核', color: 'orange' },
  confirmed: { text: '已确认', color: 'green' },
  edited: { text: '已编辑', color: 'purple' },
}

function fieldStatusText(status: BaselineField['status']): string {
  return fieldStatusMap[status].text
}

function fieldStatusColor(status: BaselineField['status']): string {
  return fieldStatusMap[status].color
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.read-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

/* —— 上传页 —— */
.read-upload {
  height: 100%;
  overflow: auto;
  display: flex;
  flex-direction: column;
}

.read-upload__main {
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

.upload-list {
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: @spacing-xs;
}

.read-upload__footer {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: @spacing-md;
  padding: @spacing-sm 0 0;
}

.read-upload__hint {
  font-size: @font-size-xs;
  color: @text-tertiary;

  &--error {
    color: @danger;
  }
}

/* —— 工作区 —— */
.read-workspace {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.read-workspace__bar {
  display: flex;
  align-items: center;
  gap: @spacing-md;
  padding: 0 0 @spacing-md;
  flex-shrink: 0;
}

.read-workspace__name {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: @spacing-sm;
}

.read-workspace__name-title {
  flex: 0 1 auto;
  width: fit-content;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: @font-size-lg;
  font-weight: @font-weight-bold;
  color: @text-primary;
}

.read-workspace__banner {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-xs @spacing-md;
  margin-bottom: @spacing-sm;
  border: 1px solid @danger;
  border-radius: @radius-base;
  background: color-mix(in srgb, @danger 8%, @card-bg);
  font-size: @font-size-xs;
  color: @danger;
  flex-shrink: 0;
}

.read-workspace__progress {
  display: flex;
  align-items: center;
  gap: @spacing-md;
  padding: @spacing-xs @spacing-md;
  margin-bottom: @spacing-sm;
  background: @content-bg;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  font-size: @font-size-xs;
  color: @text-secondary;
  flex-shrink: 0;
}

.read-workspace__progress-label {
  flex-shrink: 0;
  font-weight: @font-weight-medium;
  color: @text-primary;
}

.read-workspace__progress-bar {
  flex: 1;
  min-width: 120px;
  margin: 0;
}

.read-workspace__progress-message {
  max-width: 40%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  color: @text-tertiary;
}

.read-workspace__split {
  flex: 1;
  min-height: 0;
  display: grid;
  grid-template-columns: 240px minmax(0, 1fr) 320px;
  gap: @spacing-md;
  min-height: 0;
}

.read-workspace__outline {
  min-height: 0;
  max-height: 100%;
  overflow-y: auto;
}

.read-workspace__pdf {
  min-height: 0;
  display: flex;
  flex-direction: column;

  :deep(.section-card-body) {
    flex: 1;
    min-height: 0;
    display: flex;
    flex-direction: column;
  }
}

.read-workspace__baseline {
  min-height: 0;
  max-height: 100%;
  overflow-y: auto;
}

.read-workspace__tabs {
  :deep(.ant-tabs-nav) {
    margin-bottom: @spacing-sm;
  }
  :deep(.ant-tabs-tab) {
    padding: 6px 10px;
  }
}

.read-workspace__baseline-body {
  padding: @spacing-sm 0;
}

.baseline-field {
  border: 1px solid @border-color;
  border-radius: @radius-base;
  padding: @spacing-sm @spacing-base;
  margin-bottom: @spacing-sm;
  cursor: pointer;
  transition: border-color @transition-fast, box-shadow @transition-fast;

  &:hover {
    border-color: @brand-primary;
  }

  &--active {
    border-color: @brand-primary;
    box-shadow: 0 0 0 2px color-mix(in srgb, @brand-primary 12%, transparent);
  }

  & + & {
    margin-top: @spacing-sm;
  }
}

.baseline-field__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: @spacing-sm;
  margin-bottom: @spacing-xs;
}

.baseline-field__actions {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
  flex-shrink: 0;
}

.baseline-field__key {
  font-weight: @font-weight-semibold;
  color: @text-primary;
  font-size: @font-size-sm;
  word-break: break-all;
}

.baseline-field__value {
  font-size: @font-size-sm;
  color: @text-primary;
  line-height: 1.6;
  word-break: break-all;
  margin-bottom: @spacing-xs;
}

.baseline-field__meta {
  display: flex;
  align-items: center;
  gap: @spacing-md;
  font-size: @font-size-xs;
  color: @text-tertiary;
}

.baseline-field__refs {
  display: flex;
  flex-wrap: wrap;
  gap: @spacing-xs;
  margin-top: @spacing-xs;
}

.baseline-field__ref {
  font-size: @font-size-xs;
  color: @brand-primary;
  background: color-mix(in srgb, @brand-primary 8%, transparent);
  border-radius: @radius-sm;
  padding: 2px 6px;
}

.read-outline__title {
  font-size: @font-size-sm;
  color: @text-secondary;
}

@media (max-width: 1280px) {
  .read-workspace__split {
    grid-template-columns: 200px minmax(0, 1fr);
  }
  .read-workspace__baseline {
    grid-column: 1 / -1;
    max-height: none;
  }
}

@media (prefers-reduced-motion: reduce) {
  .upload-card,
  .read-workspace__pdf,
  .read-workspace__outline,
  .read-workspace__baseline {
    transition: none;
  }
}
</style>
