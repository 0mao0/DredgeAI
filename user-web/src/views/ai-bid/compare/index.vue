<template>
  <div class="compare-page">
    <!-- 空闲态：引导创建 -->
    <div v-if="!task" class="compare-idle">
      <a-button type="primary" size="large" @click="createVisible = true">
        <PlusOutlined />创建新的任务
      </a-button>
      <p class="compare-idle__hint">历史任务可从右上角「历史记录」打开</p>
    </div>

    <!-- 分析页：展示逐步分析过程 -->
    <AnalysisView
      v-else-if="view === 'analyzing'"
      :task="task"
      :evidence="evidence"
      @completed="onEnterResult"
    />

    <!-- 结果页：左 PDFCombo 右结果 Tab，左右联动 -->
    <template v-else>
      <div class="analysis-bar">
        <span class="analysis-bar__name">{{ task.name }}</span>
        <a-tag :color="statusInfo.color">{{ statusInfo.text }}</a-tag>

        <div class="analysis-bar__actions">
          <a-button size="small" @click="view = 'analyzing'">
            <LineChartOutlined />分析过程
          </a-button>
          <a-button size="small" @click="settingsVisible = true">
            <SettingOutlined />设置
          </a-button>
          <a-button size="small" @click="createVisible = true">
            <PlusOutlined />新建任务
          </a-button>
        </div>
      </div>

      <div ref="shellRef" class="result-shell">
        <div class="result-shell__left" :style="{ width: `${splitRatio * 100}%` }">
          <PdfCombo
            ref="comboRef"
            v-model:collapsed="comboCollapsed"
            :documents="task.documents"
          />
        </div>
        <div
          class="result-shell__divider"
          :class="{ 'result-shell__divider--dragging': draggingSplit }"
          @pointerdown="onDividerDown"
        />
        <div class="result-shell__right">
          <ResultTabs
            :task="task"
            :overview="overview"
            :evidence="evidence"
            :clauses="clauses"
            :loading="resultsLoading"
            :saving-clauses="savingClauses"
            :exporting="exporting"
            @locate="onLocateEvidence"
            @locate-doc="onLocateDoc"
            @save-clauses="onSaveClauses"
            @export="exportVisible = true"
          />
        </div>
      </div>
    </template>

    <CreateTaskModal v-model:open="createVisible" @created="handleCreated" />
    <SettingsDrawer v-model:open="settingsVisible" @save="onSaveSettings" />
    <ExportModal v-model:open="exportVisible" :exporting="exporting" @confirm="handleExport" />
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch, onMounted, onUnmounted } from 'vue'
import { useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import { LineChartOutlined, PlusOutlined, SettingOutlined } from '@ant-design/icons-vue'
import CreateTaskModal from './components/CreateTaskModal.vue'
import AnalysisView from './components/AnalysisView.vue'
import PdfCombo from './components/PdfCombo.vue'
import ResultTabs from './components/ResultTabs.vue'
import SettingsDrawer from './components/SettingsDrawer.vue'
import type { CompareSettings } from './components/SettingsDrawer.vue'
import ExportModal from './components/ExportModal.vue'
import { COMPARE_STATUS_MAP } from './constants'
import {
  getTask,
  getOverview,
  getEvidence,
  getClauseLibrary,
  createTask,
  uploadDocument,
  confirmClauses,
  exportReport,
  getExportStatus,
} from '@/api/modules/compare'
import type { ClauseItem, CompareTask, CompareTaskStatus, EvidenceItem, TaskOverview } from '@/types'

const route = useRoute()

const task = ref<CompareTask | null>(null)
const view = ref<'analyzing' | 'result'>('analyzing')
const overview = ref<TaskOverview | null>(null)
const evidence = ref<EvidenceItem[]>([])
const clauses = ref<ClauseItem[]>([])
const resultsLoading = ref(false)
const savingClauses = ref(false)
const createVisible = ref(false)
const settingsVisible = ref(false)
const exportVisible = ref(false)
const exporting = ref(false)
const comboCollapsed = ref(false)
const comboRef = ref<InstanceType<typeof PdfCombo> | null>(null)
const shellRef = ref<HTMLElement | null>(null)
const splitRatio = ref(0.52)
const draggingSplit = ref(false)

// PDFCombo 收成单栏时自动收窄左栏，让结果区向左展开（用户仍可拖竖条再调）
watch(comboCollapsed, (collapsed) => {
  splitRatio.value = collapsed ? 0.3 : 0.52
})

function onDividerDown(e: PointerEvent): void {
  e.preventDefault()
  draggingSplit.value = true
  const onMove = (ev: PointerEvent): void => {
    if (!shellRef.value) return
    const rect = shellRef.value.getBoundingClientRect()
    const ratio = (ev.clientX - rect.left) / rect.width
    splitRatio.value = Math.min(0.75, Math.max(0.28, ratio))
  }
  const onUp = (): void => {
    draggingSplit.value = false
    window.removeEventListener('pointermove', onMove)
    window.removeEventListener('pointerup', onUp)
  }
  window.addEventListener('pointermove', onMove)
  window.addEventListener('pointerup', onUp)
}

let pollTimer: ReturnType<typeof setInterval> | null = null
let evidenceRequested = false

const statusInfo = computed(() => task.value ? COMPARE_STATUS_MAP[task.value.status] : { color: 'default', text: '' })

onMounted(() => {
  const id = route.query.task
  if (typeof id === 'string' && id) void openTask(id)
})

watch(() => route.query.task, (id) => {
  if (typeof id === 'string' && id && id !== task.value?.id) void openTask(id)
})

onUnmounted(stopPoll)

function stopPoll(): void {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
}

function isTerminal(s: CompareTaskStatus): boolean {
  return s === 'completed' || s === 'partial' || s === 'failed'
}

async function openTask(id: string): Promise<void> {
  stopPoll()
  resetResults()
  try {
    const t = await getTask(id)
    task.value = t
    if (isTerminal(t.status)) {
      view.value = 'result'
      await loadResults()
    } else {
      view.value = 'analyzing'
      startPoll()
    }
  } catch {
    message.error('任务加载失败，请稍后重试')
  }
}

function startPoll(): void {
  stopPoll()
  pollTimer = setInterval(async () => {
    if (!task.value) return
    try {
      const t = await getTask(task.value.id)
      task.value = t
      if (!evidenceRequested && (t.status === 'comparing' || t.status === 'ai_analyzing' || t.status === 'completed')) {
        evidenceRequested = true
        void loadEvidence()
      }
      if (isTerminal(t.status)) {
        stopPoll()
        await loadResults()
      }
    } catch {
      stopPoll()
    }
  }, 3000)
}

async function loadResults(): Promise<void> {
  if (!task.value) return
  const id = task.value.id
  resultsLoading.value = true
  try {
    const [ov, ev, cl] = await Promise.all([getOverview(id), getEvidence(id), getClauseLibrary()])
    overview.value = ov
    evidence.value = ev
    clauses.value = cl.filter((c) => c.mandatory)
  } catch {
    message.error('结果数据加载失败，请稍后重试')
  } finally {
    resultsLoading.value = false
  }
}

async function loadEvidence(): Promise<void> {
  if (!task.value) return
  try {
    evidence.value = await getEvidence(task.value.id)
  } catch {
    /* 证据加载失败不阻塞进度展示 */
  }
}

function resetResults(): void {
  overview.value = null
  evidence.value = []
  clauses.value = []
  evidenceRequested = false
  resultsLoading.value = false
}

async function handleCreated(payload: { name: string, files: File[], tenderFile?: File }): Promise<void> {
  try {
    const t = await createTask(payload.name)
    task.value = t
    const uploads: Promise<unknown>[] = []
    if (payload.tenderFile) uploads.push(uploadDocument(t.id, payload.tenderFile, 'tender'))
    for (const f of payload.files) uploads.push(uploadDocument(t.id, f, 'bid'))
    const results = await Promise.allSettled(uploads)
    const failedCount = results.filter((r) => r.status === 'rejected').length
    if (failedCount > 0) {
      message.warning(`${failedCount} 份文件上传失败，请稍后在历史记录中重试`)
    }
    task.value = await getTask(t.id)
    resetResults()
    view.value = 'analyzing'
    startPoll()
  } catch {
    message.error('任务创建失败，请稍后重试')
  }
}

function onEnterResult(): void {
  view.value = 'result'
  if (!overview.value && !resultsLoading.value) void loadResults()
}

function onLocateEvidence(ev: EvidenceItem): void {
  comboRef.value?.locate(ev)
}

function onLocateDoc(payload: { docId: string, page: number }): void {
  comboRef.value?.locateDoc(payload.docId, payload.page)
}

async function onSaveClauses(list: ClauseItem[]): Promise<void> {
  if (!task.value) return
  savingClauses.value = true
  try {
    await confirmClauses({
      taskId: task.value.id,
      clauses: list.map((c) => ({
        clauseId: c.id,
        title: c.title,
        content: c.content,
        mandatory: c.mandatory,
      })),
    })
    clauses.value = list.map((c) => ({ ...c }))
    message.success('要求已保存')
  } catch {
    message.error('保存失败，请稍后重试')
  } finally {
    savingClauses.value = false
  }
}

function onSaveSettings(_settings: CompareSettings): void {
  message.success('设置已保存')
}

async function handleExport(format: 'docx' | 'pdf'): Promise<void> {
  if (!task.value || exporting.value) return
  exporting.value = true
  message.loading({ content: '报告生成中…', key: 'compare-export', duration: 0 })
  try {
    const job = await exportReport(task.value.id, format)
    exportVisible.value = false
    pollExport(task.value.id, job.exportId)
  } catch {
    exporting.value = false
    message.error({ content: '导出请求失败', key: 'compare-export' })
  }
}

function pollExport(taskId: string, exportId: string): void {
  const timer = setInterval(async () => {
    try {
      const st = await getExportStatus(taskId, exportId)
      if (st.status === 'done') {
        clearInterval(timer)
        exporting.value = false
        message.success({ content: '报告已生成', key: 'compare-export' })
        if (st.downloadUrl) window.open(st.downloadUrl, '_blank')
      } else if (st.status === 'failed') {
        clearInterval(timer)
        exporting.value = false
        message.error({ content: '导出失败，可重试', key: 'compare-export' })
      }
    } catch {
      clearInterval(timer)
      exporting.value = false
      message.error({ content: '导出状态查询失败', key: 'compare-export' })
    }
  }, 1500)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.compare-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.compare-idle {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: @spacing-md;

  &__hint {
    font-size: @font-size-xs;
    color: @text-tertiary;
    margin: 0;
  }
}

.analysis-bar {
  display: flex;
  align-items: center;
  gap: @spacing-md;
  margin-bottom: @spacing-md;
  flex-shrink: 0;

  &__name {
    font-size: @font-size-lg;
    font-weight: @font-weight-semibold;
    color: @text-primary;
    white-space: nowrap;
  }

  &__actions {
    margin-left: auto;
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    flex-shrink: 0;
  }
}

.result-shell {
  flex: 1;
  min-height: 0;
  display: flex;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-lg;
  overflow: hidden;

  &__left {
    flex-shrink: 0;
    min-width: 0;
    padding: @spacing-md;
    display: flex;
    flex-direction: column;
  }

  &__divider {
    flex-shrink: 0;
    width: 3px;
    cursor: col-resize;
    position: relative;

    // 隐形扩大拖拽热区，不影响 3px 视觉间隙
    &::before {
      content: '';
      position: absolute;
      top: 0;
      bottom: 0;
      left: -4px;
      right: -4px;
    }

    &::after {
      content: '';
      position: absolute;
      top: 0;
      bottom: 0;
      left: 1px;
      width: 1px;
      background: @border-color;
      transition: background @transition-fast;
    }

    &:hover::after,
    &--dragging::after {
      background: @brand-primary;
    }
  }

  &__right {
    flex: 1;
    min-width: 0;
    display: flex;
    flex-direction: column;
  }
}
</style>
