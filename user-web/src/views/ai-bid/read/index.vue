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
        <div class="read-workspace__name" @mouseleave="cancelEditName">
          <template v-if="projectNameVisible">
            <span
              v-if="!editingName"
              class="read-workspace__name-title"
              title="悬停编辑项目名"
              @mouseenter="startEditName"
            >{{ task.name }}</span>
            <template v-else>
              <a-input
                ref="nameInputRef"
                v-model:value="nameDraft"
                :maxlength="128"
                class="read-workspace__name-input"
                :loading="nameSaving"
                @input="nameDraftTouched = true"
                @press-enter="saveName"
              />
              <AppButton
                size="sm"
                variant="primary"
                :loading="nameSaving"
                :disabled="!canConfirmName"
                @click="saveName"
              >
                保存
              </AppButton>
              <AppButton size="sm" :disabled="nameSaving" @click="cancelEditName">取消</AppButton>
              <span v-if="nameError" class="read-workspace__name-error">{{ nameError }}</span>
            </template>
          </template>
          <a-tag v-if="task.status !== 'ready'" :color="statusColor(task.status)">{{ statusText(task.status) }}</a-tag>
          <a-tag>基准库 v{{ task.baselineVersion }}</a-tag>
        </div>
        <a-popconfirm
          title="整体重解将覆盖所有类别的现有字段（含已确认内容），确定继续吗？"
          ok-text="确定"
          cancel-text="取消"
          :overlay-inner-style="{ width: '280px' }"
          @confirm="onRecoverExtract"
        >
          <AppButton size="sm" :loading="fullReExtracting" :disabled="!canFullReExtract">
            <RedoOutlined />
            整体重解
          </AppButton>
        </a-popconfirm>
        <a-popconfirm
          title="确定新建读标？当前任务已保存，可在历史记录中继续查看。"
          ok-text="确定"
          cancel-text="取消"
          :overlay-inner-style="{ width: '280px' }"
          @confirm="backToUpload"
        >
          <AppButton size="sm">
            <PlusOutlined />
            新建
          </AppButton>
        </a-popconfirm>
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

      <div v-if="parsedStuck" class="read-workspace__banner read-workspace__banner--warning">
        <WarningOutlined />
        <span>解析已完成，但抽取任务长时间未自动启动，可点击顶部「整体重解」手动触发。</span>
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

        <!-- 中：PDF 预览（PdfViewer 自带卡片外观与空态提示，不套外框） -->
        <div class="read-workspace__pdf">
          <PdfViewer
            :file-url="documentFileUrl"
            :title="documentTitle"
            header-title="招标文件"
            :page="pdfPage"
            :high="pdfHighlights"
            :active-highlight-id="selectedFieldId || undefined"
            hide-original-label
            show-side-panel-toggle
            :side-panel-open="pdfSidePanelOpen"
            :parse-status="task.status"
            :parse-stage="documents[0]?.parseStage || task.progressStage"
            :parse-step="documents[0]?.parseStageMessage || undefined"
            :parse-error="task.failureReason || documents[0]?.parseError || undefined"
            @update:side-panel-open="pdfSidePanelOpen = $event"
            @update:page="pdfPage = $event"
            @select-highlight="onPdfHighlightSelect"
          >
            <template #side-panel>
              <div class="read-pdf-side-panel">
                <!-- 复用 docs-ui 的解析视图组合（Markdown / 树形 / 知识图谱），不自建右栏 -->
                <PDFParsedViewerCombo
                  v-model:active-tab="parsedPanelTab"
                  :markdown-content="parsedDocument?.content ?? ''"
                  :structured-items="structuredItems"
                  :index-summary-stats="indexSummaryStats"
                  :has-parsed-content="Boolean(parsedDocument?.content)"
                  :content-scroll-percent="0"
                  :active-linked-item-id="null"
                  :active-line-range="null"
                  :source-file-path="documentFileUrl"
                  :graph-data="null"
                  :dark="isDark"
                />
              </div>
            </template>
          </PdfViewer>
        </div>

        <!-- 右：基准库 -->
        <SectionCard title="基准库" class="read-workspace__baseline">
          <template #extra>
            <a-segmented
              v-if="isEnglishDocument"
              v-model:value="fieldLang"
              size="small"
              :options="[{ label: '中文', value: 'zh' }, { label: 'English', value: 'en' }]"
              class="read-workspace__lang-toggle"
            />
            <a-popconfirm
              title="解析将覆盖该类别的现有字段（含已确认内容），确定继续吗？"
              ok-text="确定"
              cancel-text="取消"
              :overlay-inner-style="{ width: '280px' }"
              @confirm="reExtractCategory(activeCategory)"
            >
              <AppButton size="sm" :loading="reExtracting" :disabled="!hasActiveCategory">
                <ReloadOutlined />
              </AppButton>
            </a-popconfirm>
            <AppButton size="sm" variant="primary" :loading="exporting" @click="exportBaseline">
              <DownloadOutlined />
              导出
            </AppButton>
          </template>
          <div v-if="extractInProgress" class="read-workspace__extract-progress">
            <div class="read-workspace__extract-progress-row">
              <span class="read-workspace__extract-progress-title">
                <LoadingOutlined spin class="read-workspace__extract-progress-spin" />
                {{ extractProgressText }}
              </span>
              <span class="read-workspace__extract-progress-percent">{{ task.progressPercent }}%</span>
            </div>
            <a-progress
              :percent="task.progressPercent"
              :show-info="false"
              size="small"
              class="read-workspace__extract-progress-bar"
            />
            <div class="read-workspace__extract-progress-hint">
              AI 分析通常需要 1~3 分钟，完成后基准库会自动刷新，无需手动操作
            </div>
          </div>
          <div v-if="baseline.length === 0" class="read-workspace__baseline-empty">
            <DataSkeleton v-if="baselineLoading || (task && isPollingStatus(task.status))" :rows="4" />
            <a-empty v-else description="未抽取到任何基准库字段">
              <p class="read-workspace__baseline-empty-tip">
                文档可能不含可抽取内容，可点击右上角「解析」重试，或新建读标重新上传。
              </p>
            </a-empty>
          </div>
          <a-collapse
            v-else
            v-model:active-key="activeCategoryKeys"
            class="read-workspace__collapse"
            :bordered="false"
          >
            <a-collapse-panel
              v-for="cat in baselineCategoryOptions"
              :key="cat.value"
            >
              <template #header>
                <span class="read-workspace__cat-label">{{ categoryLabel(cat.value) }}</span>
                <a-tag class="read-workspace__cat-count">{{ fieldsOf(cat.value).length }} 项</a-tag>
              </template>
              <div v-if="baselineLoading" class="read-workspace__baseline-body">
                <DataSkeleton :rows="4" />
              </div>
              <a-empty v-else-if="fieldsOf(cat.value).length === 0" description="该分类暂无字段" />
              <div v-else class="read-workspace__baseline-body">
                <div
                  v-for="field in fieldsOf(cat.value)"
                  :key="field.id"
                  class="baseline-field"
                  :class="{ 'baseline-field--active': selectedFieldId === field.id }"
                  @click="locateField(field)"
                >
                  <div class="baseline-field__head">
                    <span class="baseline-field__key">{{ fieldLabel(field) }}</span>
                    <div class="baseline-field__meta">
                      <a-tooltip :title="`置信度 ${Math.round(field.confidence * 100)}%`">
                        <a-tag
                          class="baseline-field__extractor"
                          :class="`baseline-field__extractor--${field.extractor}`"
                        >
                          {{ extractorLabel(field.extractor) }}
                        </a-tag>
                      </a-tooltip>
                    </div>
                  </div>
                  <div v-if="editingFieldId === field.id" class="baseline-field__edit" @click.stop>
                    <a-textarea
                      v-model:value="editFieldDraft"
                      :auto-size="{ minRows: 2, maxRows: 6 }"
                    />
                    <div v-if="hasMandatoryField(field)" class="baseline-field__edit-row">
                      <span class="baseline-field__edit-label">强制</span>
                      <a-switch
                        :checked="editFieldMandatory === true"
                        size="small"
                        @change="(v: boolean) => editFieldMandatory = v"
                      />
                    </div>
                    <div v-if="hasCategoryField(field)" class="baseline-field__edit-row">
                      <span class="baseline-field__edit-label">分类</span>
                      <a-select
                        v-model:value="editFieldCategory"
                        :options="clauseCategoryOptions"
                        allow-clear
                        show-search
                        placeholder="选择分类"
                        size="small"
                        style="width: 180px"
                      />
                    </div>
                  </div>
                  <div v-else class="baseline-field__value">
                    <template v-if="fieldValueParts(field).text">
                      <span class="baseline-field__value-text"><template v-if="fieldValueParts(field).textKey === 'text'"><span class="baseline-field__label">条款：</span></template>{{ fieldValueParts(field).text }}</span>
                      <template v-if="field.sourceRefs.length">
                        <span
                          v-for="(source, i) in field.sourceRefs"
                          :key="`${source.fieldId}-${i}`"
                          class="baseline-field__ref-circle"
                          :title="`第 ${source.pageIdx + 1} 页`"
                          @click.stop="locateSource(source)"
                        >{{ sourceRefNumber(field, i) }}</span>
                      </template>
                      <div
                        v-if="hasMandatoryField(field)"
                        class="baseline-field__category"
                      >
                        <span class="baseline-field__label">强制：</span><span
                          :class="{ 'baseline-field__mandatory-yes': fieldValueParts(field).mandatory === true }"
                        >{{ fieldValueParts(field).mandatory === true ? '是' : '否' }}</span>
                      </div>
                      <div
                        v-if="fieldValueParts(field).category"
                        class="baseline-field__category"
                      >
                        <span class="baseline-field__label">分类：</span>{{ fieldValueParts(field).category }}
                      </div>
                      <div
                        v-for="extra in fieldValueParts(field).extras"
                        :key="extra.label"
                        class="baseline-field__category"
                      >
                        {{ extra.label }}：{{ extra.value }}
                      </div>
                    </template>
                    <template v-else-if="fieldValueParts(field).list.length">
                      <div
                        v-for="(item, i) in fieldValueParts(field).list"
                        :key="i"
                        class="baseline-field__list-item"
                      >
                        <span class="baseline-field__value-text">{{ item }}</span>
                        <span
                          v-if="field.sourceRefs[i]"
                          class="baseline-field__ref-circle"
                          :title="`第 ${field.sourceRefs[i].pageIdx + 1} 页`"
                          @click.stop="locateSource(field.sourceRefs[i])"
                        >{{ sourceRefNumber(field, i) }}</span>
                      </div>
                    </template>
                    <template v-else>
                      {{ formatFieldValue(field.valueJson) }}
                      <template v-if="field.sourceRefs.length">
                        <span
                          v-for="(source, i) in field.sourceRefs"
                          :key="`${source.fieldId}-${i}`"
                          class="baseline-field__ref-circle"
                          :title="`第 ${source.pageIdx + 1} 页`"
                          @click.stop="locateSource(source)"
                        >{{ sourceRefNumber(field, i) }}</span>
                      </template>
                    </template>
                  </div>
                  <div class="baseline-field__footer">
                    <template v-if="editingFieldId === field.id">
                      <AppButton
                        size="sm"
                        variant="primary"
                        :loading="editFieldSaving"
                        @click.stop="saveEditField(field)"
                      >
                        确认
                      </AppButton>
                      <AppButton size="sm" :disabled="editFieldSaving" @click.stop="cancelEditField">
                        取消
                      </AppButton>
                    </template>
                    <AppButton
                      v-else
                      variant="link"
                      size="sm"
                      @click.stop="startEditField(field)"
                    >
                      修改
                    </AppButton>
                  </div>
                </div>
              </div>
            </a-collapse-panel>
          </a-collapse>
        </SectionCard>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  DownloadOutlined,
  ExclamationCircleOutlined,
  InboxOutlined,
  LoadingOutlined,
  PlusOutlined,
  RedoOutlined,
  ReloadOutlined,
  WarningOutlined,
} from '@ant-design/icons-vue'
import { AppButton, DataSkeleton, SectionCard, UploadFileRow } from '@shared/web'
import type { BaselineCategory, BaselineField, BlockRange, SourceRef, TenderReadingDocument, TenderReadingOutlineNode, TenderReadingParsedDocument, TenderReadingTask, UploadFileItem } from '@/types'
import PdfViewer from '../compare/components/PdfViewer.vue'
import { PDFParsedViewerCombo } from '@angineer/docs-ui'
import type { PreviewMode, StructuredIndexItem } from '@angineer/docs-ui'
import { useThemeStore } from '@shared/web/stores'
import {
  createTenderReadTask,
  deleteTenderReadTask,
  exportTenderReadBaseline,
  getTenderReadBaseline,
  getTenderReadDocumentFileUrl,
  getTenderReadDocuments,
  getTenderReadOutline,
  getTenderReadParsedDocument,
  getTenderReadTask,
  reExtractTenderReadBaseline,
  reparseTenderReadTask,
  startTenderReadParse,
  updateTenderReadField,
  updateTenderReadTask,
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
const parsedDocument = ref<TenderReadingParsedDocument | null>(null)
const parsedPanelTab = ref<PreviewMode>('Preview_Markdown')
const pdfSidePanelOpen = ref(false)
const exporting = ref(false)
const pdfPage = ref(1)
const pdfHighlights = ref<BlockRange[]>([])
const selectedFieldId = ref('')
/** 默认仅展开「项目信息」，其余分类折叠，保持基准库界面整洁 */
const activeCategoryKeys = ref<BaselineCategory[]>(['project_info'])
const activeCategory = computed(() => activeCategoryKeys.value[0] ?? 'project_info')
const hasActiveCategory = computed(() => activeCategoryKeys.value.length > 0)

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
/** 全库递增的溯源圆圈序号：按展示顺序（分类 → 字段 → 溯源）编号，跨字段不重置 */
const sourceRefNumbers = computed(() => {
  const map = new Map<string, number>()
  let counter = 1
  for (const cat of baselineCategoryOptions) {
    for (const field of fieldsOf(cat.value)) {
      field.sourceRefs.forEach((_, index) => {
        map.set(`${field.id}-${index}`, counter)
        counter += 1
      })
    }
  }
  return map
})
/** 字段/分类展示语言：仅英文招标文件时右上角出现切换 */
const fieldLang = ref<'zh' | 'en'>('zh')
const isEnglishDocument = computed(() => {
  const texts: string[] = []
  for (const field of baseline.value) {
    if (field.rawText) texts.push(field.rawText)
    try {
      const parsed = JSON.parse(field.valueJson) as unknown
      texts.push(typeof parsed === 'string' ? parsed : JSON.stringify(parsed))
    } catch {
      if (field.valueJson) texts.push(field.valueJson)
    }
  }
  const all = texts.join(' ').trim()
  if (!all) return false
  const cjk = (all.match(/[\u4e00-\u9fff\u3400-\u4dbf]/g) || []).length
  return cjk / all.length < 0.1
})
const reExtracting = ref(false)
const reparsing = ref(false)
const fullReExtracting = ref(false)
/** 解析完成但抽取长时间未启动（入队失败/后台任务崩溃）时给出可操作提示 */
const PARSED_STUCK_MS = 90_000
const parsedStuck = ref(false)
let parsedStuckTimer: number | null = null
const editingName = ref(false)
const nameDraft = ref('')
const nameDraftTouched = ref(false)
const nameSaving = ref(false)
const nameError = ref('')
const nameInputRef = ref<{ focus: () => void } | null>(null)
const editingFieldId = ref('')
const editFieldDraft = ref('')
const editFieldMandatory = ref(false)
const editFieldCategory = ref<string | null>(null)
const editFieldSaving = ref(false)
let pollTimer: number | null = null
let pollGen = 0
let pollInFlight = false
let disposed = false

const POLL_MS = 3000
const route = useRoute()
const router = useRouter()
const themeStore = useThemeStore()
const isDark = computed(() => themeStore.isDark)

const canStart = computed(() => uploadItem.value?.status === 'done' && !!currentTaskId.value)
const outlineTreeData = computed<TreeData[]>(() => outline.value.map(toTreeData))
const documentFileUrl = computed(() => {
  return task.value && documents.value.length > 0 ? getTenderReadDocumentFileUrl(task.value.id) : ''
})
const documentTitle = computed(() => documents.value[0]?.fileName ?? '')
const structuredItems = computed<StructuredIndexItem[]>(() =>
  (parsedDocument.value?.ir.blocks ?? []).map((block, index) => {
    const text = (block.text || '').trim()
    return {
      id: block.blockId,
      item_type: block.type || 'segment',
      title: text || `${block.type || 'block'} @ P${block.pageIdx + 1}`,
      content: text,
      order_index: index + 1,
      meta: {
        page_seq: block.pageIdx + 1,
        block_seq: 0,
        source: 'ir',
      },
    }
  }),
)
const indexSummaryStats = computed(() => {
  const blocks = parsedDocument.value?.ir.blocks ?? []
  const count = (type: string): number => blocks.filter((b) => b.type === type).length
  const paragraph = count('para')
  const title = count('title')
  const table = count('table')
  const formula = count('formula')
  const figure = count('figure') + count('image')
  const headerFooter = count('header_footer')
  const total = blocks.length
  return {
    total,
    paragraph,
    title,
    table,
    formula,
    figure,
    headerFooter,
    other: Math.max(0, total - paragraph - title - table - formula - figure - headerFooter),
    maxLevel: blocks.reduce((max, b) => Math.max(max, Number(b.textLevel) || 0), 0),
  }
})
const projectNameVisible = computed(() => !!task.value)
const canConfirmName = computed(() => {
  const next = nameDraft.value.trim()
  return next.length > 0 && (nameDraftTouched.value || next !== task.value?.name)
})

/** 抽取阶段（解析完成→抽取中→复核）：在右侧基准库面板内展示抽取进度 */
const extractInProgress = computed(() => {
  const status = task.value?.status
  return status === 'parsed' || status === 'extracting' || status === 'reviewing'
})

const extractProgressText = computed(() => {
  switch (task.value?.status) {
    case 'parsed':
      return '解析完成，正在准备抽取基准库字段'
    case 'extracting':
      return '正在抽取基准库字段（AI 分析中）'
    case 'reviewing':
      return '正在复核低置信度字段'
    default:
      return ''
  }
})

/** 全量重新抽取可用条件：解析/抽取/失败中不可触发（后端同样会拒绝） */
const canFullReExtract = computed(() => {
  const status = task.value?.status
  return !!status && !['uploading', 'parsing', 'extracting', 'failed'].includes(status)
})

watch(() => task.value?.status, (status) => {
  if (!status) {
    clearParsedStuckTimer()
    return
  }
  if (status === 'parsed') {
    startParsedStuckTimer()
  } else {
    clearParsedStuckTimer()
  }
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
  clearParsedStuckTimer()
  stopPolling()
})

function startParsedStuckTimer(): void {
  clearParsedStuckTimer()
  parsedStuck.value = false
  parsedStuckTimer = window.setTimeout(() => {
    parsedStuckTimer = null
    parsedStuck.value = true
  }, PARSED_STUCK_MS)
}

function clearParsedStuckTimer(): void {
  if (parsedStuckTimer !== null) {
    window.clearTimeout(parsedStuckTimer)
    parsedStuckTimer = null
  }
  parsedStuck.value = false
}

// 从历史记录 / 外部链接打开指定读标任务（immediate 覆盖首次挂载，watch 覆盖页面内切换任务）
watch(() => route.query.task, (id) => {
  // 自身上传流程会把 task 写入地址栏，此时 currentTaskId 已存在，跳过避免重复加载
  if (typeof id === 'string' && id && id !== currentTaskId.value) void openExistingTask(id)
}, { immediate: true })

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
  void router.replace({ query: {} })
}

async function startRead(): Promise<void> {
  if (!currentTaskId.value || starting.value) return
  starting.value = true
  try {
    const t = await startTenderReadParse(currentTaskId.value)
    task.value = t
    view.value = 'workspace'
    uploadItem.value = null
    // 把任务写进地址栏：刷新/重新进入页面后仍停留在工作区而不是回到上传页
    void router.replace({ query: { task: t.id } })
    await loadDetail(t.id)
  } catch (error) {
    message.error(error instanceof Error ? error.message : '开始解析失败')
  } finally {
    starting.value = false
  }
}

/** 从历史记录打开已有读标任务（?task=id） */
async function openExistingTask(id: string): Promise<void> {
  try {
    await loadDetail(id)
    currentTaskId.value = id
    view.value = 'workspace'
    void router.replace({ query: { task: id } })
  } catch {
    message.error('读标任务加载失败')
  }
}

function backToUpload(): void {
  stopPolling()
  clearParsedStuckTimer()
  view.value = 'upload'
  task.value = null
  currentTaskId.value = ''
  uploadItem.value = null
  documents.value = []
  outline.value = []
  baseline.value = []
  parsedDocument.value = null
  pdfHighlights.value = []
  selectedFieldId.value = ''
  void router.replace({ query: {} })
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

    await loadParsedDocument(id)
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

async function loadParsedDocument(id: string, stale?: () => boolean): Promise<void> {
  if (!task.value) return
  if (task.value.status === 'uploading' || task.value.status === 'parsing') return

  try {
    const data = await getTenderReadParsedDocument(id, true)
    if (stale?.()) return
    parsedDocument.value = data
  } catch {
    // 解析产物未就绪或读取失败时保留旧值，轮询下一轮再试
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

    await loadParsedDocument(id, stale)
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

/** 由 IR 块构建“标题 → 正文块”的结构树，用于右侧图谱/结构视图。 */
function fieldsOf(category: BaselineCategory): BaselineField[] {
  return baseline.value.filter((f) => f.category === category)
}

function sourceRefNumber(field: BaselineField, index: number): number {
  return sourceRefNumbers.value.get(`${field.id}-${index}`) ?? index + 1
}

/** 废标条款等的分类选项（LLM 输出的 category 取值） */
const clauseCategoryOptions = [
  { value: '资质', label: '资质' },
  { value: '报价', label: '报价' },
  { value: '技术', label: '技术' },
  { value: '工期', label: '工期' },
  { value: '格式', label: '格式' },
  { value: '诚信', label: '诚信' },
  { value: '商务', label: '商务' },
]

interface FieldValueParts {
  parsed: Record<string, unknown> | null
  text: string
  textKey: 'text' | 'value' | null
  list: string[]
  mandatory: boolean | null
  category: string | null
  extras: Array<{ label: string, value: string }>
}

/** 解析字段 valueJson 为结构化展示/编辑所需的数据 */
/** 提取结果对象里的英文键 → 中文标签（值内容优先展示原文，元数据用中文说明） */
const VALUE_KEY_LABELS: Record<string, string> = {
  text: '原文',
  value: '值',
  mandatory: '是否强制',
  category: '分类',
  dimension: '评分维度',
  score: '分值',
  subItems: '子项',
  deductionRules: '扣分规则',
  name: '参数名',
  requiredValue: '要求值',
  unit: '单位',
  substantive: '实质性要求',
  rules: '规则',
}
function parseFieldValue(valueJson: string): FieldValueParts {
  try {
    const raw = JSON.parse(valueJson) as unknown
    if (raw && typeof raw === 'object' && !Array.isArray(raw)) {
      const obj = raw as Record<string, unknown>
      const textEntry = Object.entries(obj).find(([key]) => key === 'text' || key === 'value')
      const text = textEntry ? formatValue(textEntry[1]) : ''
      const textKey = textEntry ? (textEntry[0] === 'text' ? 'text' : 'value') : null
      const listEntry = Object.entries(obj).find(([, val]) => Array.isArray(val))
      const list = listEntry && Array.isArray(listEntry[1])
        ? (listEntry[1] as unknown[]).map((item) => formatValue(item))
        : []
      const mandatory = typeof obj.mandatory === 'boolean' ? obj.mandatory : null
      const category = typeof obj.category === 'string' && obj.category.trim()
        ? obj.category
        : null
      const extras = Object.entries(obj)
        .filter(([key]) => key !== 'text' && key !== 'value' && key !== 'mandatory' && key !== 'category')
        .filter(([, val]) => !Array.isArray(val))
        .map(([key, val]) => ({ label: VALUE_KEY_LABELS[key] || key, value: formatValue(val) }))
      return { parsed: obj, text, textKey, list, mandatory, category, extras }
    }
  } catch {
    // 非 JSON，按纯文本处理
  }
  return { parsed: null, text: valueJson, textKey: null, list: [], mandatory: null, category: null, extras: [] }
}

function fieldValueParts(field: BaselineField): FieldValueParts {
  return parseFieldValue(field.valueJson)
}

function hasMandatoryField(field: BaselineField): boolean {
  const parsed = parseFieldValue(field.valueJson).parsed
  return parsed != null && 'mandatory' in parsed
}

function hasCategoryField(field: BaselineField): boolean {
  const parsed = parseFieldValue(field.valueJson).parsed
  return parsed != null && 'category' in parsed
}

function formatFieldValue(valueJson: string): string {
  try {
    return formatValue(JSON.parse(valueJson) as unknown)
  } catch {
    return valueJson
  }
}

function formatValue(value: unknown): string {
  if (typeof value === 'string') return value
  if (value === null || value === undefined) return ''
  if (typeof value === 'boolean') return value ? '是' : '否'
  if (typeof value === 'number') return String(value)
  if (Array.isArray(value)) {
    return value.map((item) => formatValue(item)).join('；')
  }
  if (typeof value === 'object') {
    const entries = Object.entries(value as Record<string, unknown>)
    const mainEntry = entries.find(([key]) => key === 'text' || key === 'value')
    if (mainEntry) {
      const extras = entries
        .filter(([key]) => key !== mainEntry[0])
        .map(([key, val]) => `${VALUE_KEY_LABELS[key] || key}：${formatValue(val)}`)
        .join('；')
      const main = formatValue(mainEntry[1])
      return extras ? `${main}\n${extras}` : main
    }
    return entries
      .map(([key, val]) => `${VALUE_KEY_LABELS[key] || key}：${formatValue(val)}`)
      .join('\n')
  }
  return String(value)
}

function locateField(field: BaselineField): void {
  selectedFieldId.value = field.id
  pdfHighlights.value = field.sourceRefs.map(sourceRefToBlockRange)
  activeCategoryKeys.value = [...new Set([...activeCategoryKeys.value, field.category])]
  const first = field.sourceRefs[0]
  if (first) pdfPage.value = first.pageIdx + 1
}

/** 点击引用圆圈：只定位到该条溯源（页码 + 对应 bbox 高亮） */
function locateSource(source: SourceRef): void {
  selectedFieldId.value = source.fieldId
  pdfHighlights.value = [sourceRefToBlockRange(source)]
  pdfPage.value = source.pageIdx + 1
}

function autoHighlightFirstField(): void {
  if (selectedFieldId.value || pdfHighlights.value.length > 0) return
  const field = baseline.value.find((f) => f.sourceRefs.length > 0)
  if (field) {
    // 仅设置高亮与页码，不展开其所在分类（保持默认只展开「项目信息」）
    selectedFieldId.value = field.id
    pdfHighlights.value = field.sourceRefs.map(sourceRefToBlockRange)
    const first = field.sourceRefs[0]
    if (first) pdfPage.value = first.pageIdx + 1
  }
}

function onPdfHighlightSelect(highlight: { itemId?: string }): void {
  if (!highlight.itemId) return
  const field = baseline.value.find((f) => f.id === highlight.itemId)
  if (!field) return
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

/* —— 项目名（悬停进入编辑态，离开恢复标题） —— */
function startEditName(): void {
  if (!task.value || editingName.value) return
  nameDraft.value = task.value.name
  nameDraftTouched.value = false
  nameError.value = ''
  editingName.value = true
  nextTick(() => {
    nameInputRef.value?.focus()
  })
}

function cancelEditName(): void {
  if (!task.value) return
  nameDraft.value = task.value.name
  nameDraftTouched.value = false
  nameError.value = ''
  editingName.value = false
}

async function saveName(): Promise<void> {
  if (!task.value || nameSaving.value) return
  const next = nameDraft.value.trim()
  if (!next || next === task.value.name) return
  nameSaving.value = true
  nameError.value = ''
  try {
    task.value = await updateTenderReadTask(task.value.id, { name: next })
    nameDraftTouched.value = false
    nameDraft.value = next
    editingName.value = false
  } catch {
    nameError.value = '名称保存失败，可重试'
  } finally {
    nameSaving.value = false
  }
}

/* —— 基准库字段编辑 —— */
function startEditField(field: BaselineField): void {
  editingFieldId.value = field.id
  const parts = parseFieldValue(field.valueJson)
  editFieldDraft.value = parts.text || formatFieldValue(field.valueJson)
  editFieldMandatory.value = parts.mandatory === true
  editFieldCategory.value = parts.category
}

function cancelEditField(): void {
  editingFieldId.value = ''
  editFieldDraft.value = ''
  editFieldMandatory.value = false
  editFieldCategory.value = null
}

async function saveEditField(field: BaselineField): Promise<void> {
  if (!task.value || editFieldSaving.value) return
  const next = editFieldDraft.value.trim()
  if (!next) {
    message.warning('字段内容不能为空')
    return
  }
  editFieldSaving.value = true
  try {
    const parts = parseFieldValue(field.valueJson)
    let valueJson: string
    if (parts.parsed) {
      const obj: Record<string, unknown> = { ...parts.parsed }
      const hasText = 'text' in obj
      const hasValue = 'value' in obj
      if (hasText) obj.text = next
      else if (hasValue) obj.value = next
      if ('mandatory' in obj) obj.mandatory = editFieldMandatory.value === true
      if ('category' in obj) obj.category = editFieldCategory.value ?? ''
      valueJson = JSON.stringify(obj)
    } else {
      valueJson = JSON.stringify(next)
    }
    const updated = await updateTenderReadField(task.value.id, field.id, {
      valueJson,
      rawText: next,
      status: 'edited',
    })
    baseline.value = baseline.value.map((f) => (f.id === updated.id ? updated : f))
    cancelEditField()
    message.success('字段已修改')
  } catch (error) {
    message.error(error instanceof Error ? error.message : '字段修改失败')
  } finally {
    editFieldSaving.value = false
  }
}

async function onReparseTask(): Promise<void> {
  if (!task.value || reparsing.value) return
  reparsing.value = true
  parsedDocument.value = null
  try {
    // 状态回到 parsing，status watch 自动起轮询
    task.value = await reparseTenderReadTask(task.value.id)
  } catch (error) {
    message.error(error instanceof Error ? error.message : '重新解析失败')
  } finally {
    reparsing.value = false
  }
}

async function reExtractCategory(category: BaselineCategory): Promise<void> {
  if (!task.value || reExtracting.value) return
  reExtracting.value = true
  try {
    // 重抽为后台任务：返回抽取中的任务快照，状态 watch 自动起轮询，
    // 到达终态后复位按钮并刷新基准库（轮询每 tick 都会重拉基准库）
    task.value = await reExtractTenderReadBaseline(task.value.id, category)
    selectedFieldId.value = ''
    pdfHighlights.value = []
    message.success('已提交重新抽取，完成后自动刷新')
  } catch (error) {
    reExtracting.value = false
    message.error(error instanceof Error ? error.message : '重新抽取提交失败')
  }
}

/** 解析完成但抽取未自动启动时的兜底恢复：直接提交全量重抽 */
async function onRecoverExtract(): Promise<void> {
  if (!task.value || fullReExtracting.value) return
  fullReExtracting.value = true
  try {
    task.value = await reExtractTenderReadBaseline(task.value.id)
    parsedStuck.value = false
    selectedFieldId.value = ''
    pdfHighlights.value = []
    message.success('已提交整体重解，完成后自动刷新')
  } catch (error) {
    message.error(error instanceof Error ? error.message : '整体重解提交失败')
  } finally {
    fullReExtracting.value = false
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

/** 字段 key → 中文标签（规则字段 + LLM 动态生成的常见 key 全覆盖） */
const FIELD_LABELS: Record<string, string> = {
  name: '项目名称',
  code: '项目编号',
  price_ceiling: '最高限价',
  construction_period: '工期',
  warranty_period: '质保期',
  payment_method: '付款方式',
  outline: '章节框架',
  seal_rules: '签章规则',
  dark_bid_format_rules: '暗标格式',
  bid_price: '投标报价',
  demand_understanding: '需求理解',
  key_personnel: '关键人员',
  management_and_safeguard_measures: '管理与保障措施',
  price_score: '价格得分',
  project_manager: '项目经理',
  project_team_configuration: '项目团队配置',
  schedule_plan: '进度计划',
  service_implementation_plan: '服务实施方案',
  similar_project_performance: '类似项目业绩',
  special_services_and_commitments: '专项服务与承诺',
  technical_solution: '技术方案',
  acceptance_payment_terms: '验收及付款条款',
  affiliation_prohibited: '禁止挂靠',
  bid_document_sealing: '投标文件密封',
  bid_opening_attendance_id: '开标出席身份核验',
  bid_plagiarism: '投标文件雷同',
  bid_price_below_cost: '报价低于成本',
  bid_price_exceeds_budget: '报价超出预算',
  bid_price_exceeds_limit: '报价超出限价',
  bid_security: '投标保证金',
  bid_security_deposit: '投标保证金未足额',
  bid_validity_period: '投标有效期',
  bidder_attendance_id: '投标人出席身份核验',
  clarification_failure: '澄清说明失败',
  commitment_letter_seal: '承诺书未盖章',
  commitment_letter_signature: '承诺书未签字',
  conditional_bidding: '附带条件投标',
  conflict_of_interest_designer: '设计单位利益冲突',
  credit_disqualification: '信用不良取消资格',
  excessive_missing_items: '缺项过多',
  failure_to_meet_qualification_or_substantive_requirements: '资格或实质要求不满足',
  failure_to_sign_in_or_decrypt: '未签到或未解密',
  fake_materials: '虚假材料',
  fraud_and_collusion: '弄虚作假与串通投标',
  inspection_standards: '检验标准不符',
  integrity_violation: '诚信违规',
  invalid_bid_clarification_failure: '澄清说明不符',
  invalid_bid_committee_signature: '评标委员会签字缺失',
  invalid_bid_competitive_costs: '不可竞争费用不符',
  invalid_bid_duration: '工期超限',
  invalid_bid_false_materials: '虚假材料',
  invalid_bid_fraud: '弄虚作假',
  invalid_bid_inspection_standards: '检验标准不符',
  invalid_bid_law_violation: '违法投标',
  invalid_bid_multiple_bids: '一标多投',
  invalid_bid_opening_attendance: '开标未出席',
  invalid_bid_payment_terms: '付款条款不符',
  invalid_bid_personnel_mismatch: '人员不匹配',
  invalid_bid_price_range: '报价超出范围',
  invalid_bid_qualification: '资格不符',
  invalid_bid_scheme_requirements: '方案要求不符',
  invalid_bid_security_deposit_missing: '保证金缺失',
  invalid_bid_similarities: '投标文件雷同',
  invalid_bid_substantive_response: '未实质响应',
  invalid_bid_technical_standards: '技术标准不符',
  joint_venture_not_accepted: '不接受联合体',
  late_submission: '逾期递交',
  missing_bid_letter: '投标函缺失',
  missing_qualification_response_table: '资格响应表缺失',
  missing_seals_on_critical_docs: '关键文件缺章',
  multiple_bids: '一标多投',
  no_alternative_bid: '不接受备选方案',
  no_deviation: '不得偏离',
  no_joint_venture: '不得联合体投标',
  no_subcontracting: '不得分包',
  non_competitive_fees: '不可竞争费用',
  power_of_attorney: '授权委托书',
  project_manager_consistency: '项目经理一致性',
  qualification_non_compliance: '资格不符',
  refusal_to_confirm_price_correction: '拒绝确认报价修正',
  same_legal_representative_or_control: '同一法定代表人或控制关系',
  scheme_requirements: '方案要求',
  selective_bidding: '选择性报价',
  substantial_response: '实质响应',
  technical_standards: '技术标准',
  unreasonable_low_price: '不合理低价',
  bid_price_type: '报价方式',
  bid_validity: '投标有效期',
  budget_limit: '预算限额',
  credit_requirement: '信用要求',
  delivery_time: '交付时间',
  design_depth: '设计深度',
  design_standard: '设计标准',
  joint_bidding: '联合体投标',
  payment_advance: '预付款',
  payment_final: '最终付款',
  qualification: '资格要求',
  service_period: '服务期限',
}

/** 未收录字段 key 的逐词翻译兜底：LLM 动态生成 key 无法穷举，拆词翻译避免直接显示英文 */
const FIELD_TOKEN_LABELS: Record<string, string> = {
  abnormal: '异常',
  absence: '缺席',
  absent: '缺席',
  acceptance: '验收',
  accepted: '接受',
  advance: '预',
  affiliation: '挂靠',
  alternative: '备选',
  amount: '金额',
  assets: '资产',
  attendance: '到场',
  attorney: '委托',
  authorization: '授权',
  award: '中标',
  bankruptcy: '破产',
  below: '低于',
  bid: '投标',
  bidder: '投标人',
  bidding: '投标',
  bids: '投标',
  bond: '保函',
  bribery: '行贿',
  budget: '预算',
  business: '经营',
  ceiling: '上限',
  clarification: '澄清',
  clarify: '澄清',
  code: '编号',
  collusion: '串通',
  collusive: '串通',
  commitment: '承诺',
  commitments: '承诺',
  committee: '委员会',
  competitive: '竞争性',
  compilation: '编制',
  compliance: '合规',
  compliant: '合规',
  conditional: '附带条件',
  confirm: '确认',
  configuration: '配置',
  conflict: '冲突',
  consistency: '一致性',
  consortium: '联合体',
  construction: '施工',
  content: '内容',
  contract: '合同',
  contractor: '承包',
  control: '控制',
  coordination: '协调',
  corrected: '修正',
  correction: '修正',
  corruption: '腐败',
  cost: '费用',
  costs: '费用',
  credit: '信用',
  critical: '关键',
  currency: '币种',
  dark: '暗',
  deadline: '截止',
  decrypt: '解密',
  defect: '缺陷',
  delay: '延误',
  deliverables: '交付物',
  delivery: '交付',
  demand: '需求',
  deposit: '保证金',
  depth: '深度',
  design: '设计',
  designer: '设计单位',
  deviation: '偏差',
  disqualification: '取消资格',
  disqualifications: '取消资格',
  disruption: '扰乱',
  disturbance: '扰乱',
  docs: '文件',
  document: '文件',
  documents: '文件',
  duplicate: '重复',
  duration: '工期',
  exceed: '超出',
  exceeded: '超出',
  exceeds: '超出',
  excessive: '过多',
  explicit: '明确',
  fail: '失败',
  failure: '失败',
  fake: '虚假',
  false: '虚假',
  fees: '费用',
  files: '文件',
  final: '最终',
  fixed: '固定',
  fixity: '固定性',
  forgery: '伪造',
  format: '格式',
  fraud: '舞弊',
  fraudulent: '虚假',
  freeze: '冻结',
  frozen: '冻结',
  general: '一般',
  guarantee: '担保',
  hanging: '挂靠',
  id: '证件',
  identity: '身份',
  implementation: '实施',
  inconsistency: '不一致',
  inconsistent: '不一致',
  info: '信息',
  inspection: '检验',
  integrity: '诚信',
  interest: '利益',
  interference: '干扰',
  invalid: '无效',
  invoice: '发票',
  issue: '事项',
  item: '项',
  items: '项',
  joint: '联合',
  key: '关键',
  lack: '缺少',
  late: '逾期',
  law: '法律',
  leader: '负责人',
  legal: '法律',
  letter: '函件',
  level: '等级',
  liaison: '联络',
  license: '资质',
  life: '寿命',
  limit: '限价',
  location: '地点',
  low: '低',
  major: '重大',
  management: '管理',
  manager: '经理',
  material: '材料',
  materials: '材料',
  max: '最高',
  measures: '措施',
  meet: '满足',
  meeting: '会议',
  method: '方式',
  milestone: '节点',
  misconduct: '不良行为',
  mismatch: '不匹配',
  missing: '缺失',
  multiple: '多项',
  name: '名称',
  no: '不得',
  non: '不',
  not: '不得',
  open: '开标',
  opening: '开标',
  order: '秩序',
  out: '超出',
  outline: '大纲',
  payment: '付款',
  penalty: '处罚',
  percent: '百分比',
  performance: '履约',
  period: '期限',
  personnel: '人员',
  plagiarism: '雷同',
  plan: '方案',
  post: '中标后',
  power: '授权',
  price: '报价',
  principles: '原则',
  prohibited: '禁止',
  prohibition: '禁止',
  project: '项目',
  provide: '提供',
  provider: '提供方',
  qualification: '资格',
  quality: '质量',
  quantity: '数量',
  quotation: '报价',
  range: '范围',
  rate: '费率',
  ratio: '比例',
  recent: '近期',
  record: '记录',
  refusal: '拒绝',
  regulation: '规定',
  rejection: '否决',
  rep: '代表',
  representative: '代表',
  requirement: '要求',
  requirements: '要求',
  respond: '响应',
  response: '响应',
  restriction: '限制',
  review: '评审',
  rigging: '围标',
  rule: '规则',
  rules: '规则',
  safeguard: '保障',
  safety: '安全',
  same: '同一',
  schedule: '进度',
  scheme: '方案',
  scope: '范围',
  score: '得分',
  seal: '签章',
  sealing: '密封',
  seals: '签章',
  security: '保证金',
  selective: '选择性',
  service: '服务',
  services: '服务',
  short: '过短',
  sign: '签字',
  signatory: '签署人',
  signature: '签字',
  similar: '雷同',
  similarities: '雷同',
  solution: '方案',
  source: '来源',
  special: '专项',
  specs: '参数',
  stamp: '盖章',
  standard: '标准',
  standards: '标准',
  status: '状态',
  subcontracting: '分包',
  submission: '递交',
  substantial: '实质性',
  substantive: '实质性',
  suspended: '暂停',
  suspension: '停业',
  table: '表',
  tax: '税率',
  team: '团队',
  tech: '技术',
  technical: '技术',
  terms: '条款',
  time: '时间',
  title: '职务',
  transfer: '转让',
  type: '类型',
  unacceptable: '不可接受',
  understanding: '理解',
  uniqueness: '唯一性',
  unjustified: '无依据',
  unreasonable: '不合理',
  validity: '有效期',
  variable: '可变',
  venture: '体',
  verification: '核验',
  violation: '违规',
  withdrawal: '撤回',
}

/** 纯连接词：翻译时直接省略，避免出现“的/和/或”等多余字 */
const FIELD_LABEL_SKIP_TOKENS = new Set(['after', 'and', 'during', 'in', 'of', 'on', 'or', 'to'])

/** LLM 动态字段 key → 中文标签：拆词逐词翻译后拼接，保证不出现英文 */
function translateFieldKey(fieldKey: string): string {
  const parts = fieldKey.split('_').filter((p) => p && !FIELD_LABEL_SKIP_TOKENS.has(p))
  const translated = parts.map((p) => FIELD_TOKEN_LABELS[p] ?? p).join('')
  return translated || fieldKey.replace(/_/g, ' ')
}

function fieldLabel(field: BaselineField): string {
  if (fieldLang.value === 'en') return field.fieldKey.replace(/_/g, ' ')
  const sealRule = field.fieldKey.match(/^seal_rule_(\d+)$/)
  if (sealRule) return `签章规则 ${sealRule[1]}`
  const darkBidRule = field.fieldKey.match(/^dark_bid_rule_(\d+)$/)
  if (darkBidRule) return `暗标格式 ${darkBidRule[1]}`
  const invalidBidNumber = field.fieldKey.match(/^invalid_bid_(\d+)$/)
  if (invalidBidNumber) return `废标条款 ${invalidBidNumber[1]}`
  const paymentMilestone = field.fieldKey.match(/^payment_milestone_(\d+)$/)
  if (paymentMilestone) return `付款节点 ${paymentMilestone[1]}`
  const rejectionClauseNumber = field.fieldKey.match(/^rejection_clause_(\d+)$/)
  if (rejectionClauseNumber) return `废标条款 ${rejectionClauseNumber[1]}`
  const evaluationCriteriaNumber = field.fieldKey.match(/^evaluation_criteria_(\d+)$/)
  if (evaluationCriteriaNumber) return `评分标准 ${evaluationCriteriaNumber[1]}`
  const technicalParameterNumber = field.fieldKey.match(/^technical_parameter_(\d+)$/)
  if (technicalParameterNumber) return `技术参数 ${technicalParameterNumber[1]}`
  return FIELD_LABELS[field.fieldKey] || translateFieldKey(field.fieldKey)
}

const CATEGORY_LABELS: Record<BaselineCategory, { zh: string, en: string }> = {
  project_info: { zh: '项目信息', en: 'Project Info' },
  rejection_clauses: { zh: '废标条款', en: 'Rejection Clauses' },
  evaluation_criteria: { zh: '评分标准', en: 'Evaluation Criteria' },
  technical_parameters: { zh: '技术参数', en: 'Technical Parameters' },
  commercial_data: { zh: '商务数据', en: 'Commercial Data' },
  chapter_outline: { zh: '章节框架', en: 'Chapter Outline' },
  seal_rules: { zh: '签章规则', en: 'Seal Rules' },
  dark_bid_format_rules: { zh: '暗标格式', en: 'Dark Bid Format Rules' },
}

function categoryLabel(category: BaselineCategory): string {
  const labels = CATEGORY_LABELS[category]
  return fieldLang.value === 'en' ? labels.en : labels.zh
}

function extractorLabel(extractor: string): string {
  if (extractor === 'rule') return '规则提取'
  if (extractor === 'llm') return 'AI 提取'
  return extractor
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

.read-workspace__name-input {
  width: 320px;
}

.read-workspace__name-error {
  font-size: @font-size-xs;
  color: @danger;
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

.read-workspace__banner--warning {
  border-color: @warning;
  background: color-mix(in srgb, @warning 10%, @card-bg);
  color: @warning;
}

.read-workspace__extract-progress {
  padding: @spacing-sm @spacing-md;
  margin-bottom: @spacing-md;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  background: color-mix(in srgb, @brand-primary 4%, @card-bg);

  &__row {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: @spacing-sm;
    margin-bottom: @spacing-xs;
  }

  &__title {
    display: inline-flex;
    align-items: center;
    gap: @spacing-xs;
    font-size: @font-size-sm;
    color: @text-primary;
  }

  &__spin {
    color: @brand-primary;
  }

  &__percent {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }

  &__bar {
    margin-bottom: @spacing-xs;

    :deep(.ant-progress-bg) {
      background: @brand-primary;
    }
  }

  &__hint {
    font-size: @font-size-xs;
    color: @text-tertiary;
  }
}

.read-workspace__baseline-empty {
  padding: @spacing-md 0;
}

.read-workspace__baseline-empty-tip {
  margin-top: @spacing-xs;
  font-size: @font-size-xs;
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

  :deep(.pdf-viewer) {
    flex: 1;
    min-height: 0;
  }
}

.read-pdf-side-panel {
  height: 100%;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.read-workspace__baseline {
  min-height: 0;
  max-height: 100%;
  overflow-y: auto;
}

.read-workspace__lang-toggle {
  margin-right: @spacing-sm;
}

/* 左右卡片 header 压缩（目录/基准库） */
.read-workspace__outline,
.read-workspace__baseline {
  :deep(.section-card-header) {
    padding: @spacing-sm @spacing-md;
  }
  :deep(.section-card-extra) {
    display: flex;
    align-items: center;
    gap: 2px;
  }
  :deep(.section-card-title) {
    font-size: @font-size-sm;
  }
  :deep(.section-card-body) {
    padding: @spacing-sm @spacing-md;
  }
}

.read-workspace__collapse {
  :deep(.ant-collapse-header) {
    padding: @spacing-sm @spacing-base !important;
    align-items: center;
  }
  :deep(.ant-collapse-content-box) {
    padding: @spacing-xs 0 0;
  }
}

.read-workspace__cat-label {
  margin-inline-end: @spacing-sm;
}

.read-workspace__cat-count {
  margin-inline-end: 0;
}

.read-workspace__baseline-body {
  padding: @spacing-sm 0;
}

.baseline-field {
  border: 1px solid @text-tertiary;
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
  padding-bottom: @spacing-xs;
  border-bottom: 1px solid @text-tertiary;
  margin-bottom: @spacing-sm;
}

.baseline-field__key {
  font-weight: @font-weight-bold;
  color: @brand-primary;
  font-size: @font-size-sm;
  word-break: break-all;
}

.baseline-field__value {
  font-size: @font-size-sm;
  color: @text-secondary;
  line-height: 1.6;
  word-break: break-all;
  white-space: pre-line;
  margin-bottom: @spacing-xs;
}

.baseline-field__value-text {
  white-space: pre-wrap;
}

.baseline-field__category {
  margin-top: @spacing-xs;
  font-size: @font-size-xs;
  color: @text-secondary;
}

.baseline-field__list-item {
  margin-bottom: @spacing-xs;
  &:last-child {
    margin-bottom: 0;
  }
}

.baseline-field__mandatory-yes {
  color: @danger;
  font-weight: @font-weight-semibold;
}

.baseline-field__label {
  font-weight: @font-weight-semibold;
  color: @text-primary;
}

.baseline-field__edit {
  margin-bottom: @spacing-xs;

  :deep(.ant-input) {
    font-size: @font-size-sm;
  }
}

.baseline-field__edit-row {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  margin-top: @spacing-sm;
}

.baseline-field__edit-label {
  flex-shrink: 0;
  width: 40px;
  font-size: @font-size-sm;
  color: @text-primary;
}

.baseline-field__footer {
  display: flex;
  justify-content: flex-end;
  gap: @spacing-xs;
  margin-top: @spacing-xs;
}

.baseline-field__meta {
  display: flex;
  align-items: center;
  justify-content: flex-start;
  flex-wrap: wrap;
  gap: @spacing-sm;
  font-size: @font-size-xs;
  color: @text-tertiary;

  :deep(.ant-tag) {
    margin-inline-end: 0;
  }
}

.baseline-field__extractor {
  line-height: 18px;
}

.baseline-field__extractor--llm,
.baseline-field__extractor--rule {
  background: transparent !important;
  border-color: @text-tertiary !important;
  color: @text-tertiary !important;
}

.baseline-field__ref-circle {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  vertical-align: middle;
  min-width: 14px;
  height: 14px;
  padding: 0 2px;
  margin: 0 2px;
  border-radius: 50%;
  border: 1px solid @text-tertiary;
  background: transparent;
  color: @text-tertiary;
  font-size: 8px;
  font-weight: @font-weight-semibold;
  line-height: 1;
  cursor: pointer;
  transition: border-color @transition-fast, color @transition-fast;
  &:hover {
    border-color: @brand-primary;
    color: @brand-primary;
  }
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
