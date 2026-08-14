<template>
  <div class="compare-page">
    <!-- 上传引导页 -->
    <UploadPage
      v-if="view === 'upload'"
      :name="uploadName"
      :items="uploadItems"
      :creating="creating"
      :history="historyTasks"
      :upload-error="uploadError"
      @update:name="uploadName = $event"
      @add-files="onAddFiles"
      @remove="removeItem"
      @retry="retryItem"
      @start="handleStart"
      @history-open="openTask"
    />

    <!-- 工作区：analyzing / result / failed 共用同一左右分栏 -->
    <div v-else-if="task" class="compare-workspace">
      <div class="compare-workspace__bar">
        <div class="compare-workspace__name">
          <a-input
            v-model:value="nameDraft"
            size="small"
            :maxlength="128"
            class="compare-workspace__name-input"
            :loading="nameSaving"
            @blur="saveName"
            @press-enter="saveName"
          />
          <span v-if="nameError" class="compare-workspace__name-error">{{ nameError }}</span>
        </div>
        <a-tag :color="statusInfo.color">{{ statusInfo.text }}</a-tag>

        <a-dropdown trigger="click" placement="bottomRight">
          <a-button size="small">
            <HistoryOutlined />历史任务
          </a-button>
          <template #overlay>
            <a-menu class="compare-workspace__history" @click="onHistoryClick">
              <a-menu-item v-for="t in historyTasks" :key="t.id">
                <span class="compare-workspace__history-name" :title="t.name">{{ t.name }}</span>
                <a-tag :color="COMPARE_STATUS_MAP[t.status].color">{{ COMPARE_STATUS_MAP[t.status].text }}</a-tag>
              </a-menu-item>
              <a-menu-item v-if="!historyTasks.length" key="empty" disabled>暂无历史任务</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>

        <a-dropdown
          trigger="click"
          placement="bottomRight"
          :open="exportMenuVisible"
          @open-change="exportMenuVisible = $event"
        >
          <a-button
            size="small"
            type="primary"
            :disabled="!canExport"
            :loading="exporting"
            @click.prevent
          >
            <DownloadOutlined />导出
          </a-button>
          <template #overlay>
            <a-menu @click="onExportMenuClick">
              <a-menu-item key="docx">Word 报告（.docx）</a-menu-item>
              <a-menu-item key="pdf">PDF 报告</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
        <span v-if="exportError" class="compare-workspace__export-error">{{ exportError }}</span>

        <a-tooltip :title="workspaceCollapsed ? '展开右侧面板' : '收起右侧面板'">
          <a-button size="small" @click="workspaceCollapsed = !workspaceCollapsed">
            <ExpandOutlined v-if="workspaceCollapsed" />
            <CompressOutlined v-else />
          </a-button>
        </a-tooltip>

        <a-button size="small" @click="resetToUpload()">
          <PlusOutlined />新建任务
        </a-button>
      </div>

      <div v-if="connectionLost" class="compare-workspace__banner">
        <WifiOutlined />连接中断，正在重试…
      </div>

      <div class="compare-workspace__split">
        <div
          class="compare-workspace__left"
          :style="workspaceCollapsed ? { width: '100%' } : { width: `${splitRatio * 100}%` }"
        >
          <PdfWorkspace
            ref="workspaceRef"
            :documents="workspaceDocs"
            :collapsed="workspaceCollapsed"
            :pair-active="pairActive"
            :scanning-doc-id="scanningDocId"
            @update:collapsed="workspaceCollapsed = $event"
            @tab-manual="lastManualTabAt = Date.now()"
          />
        </div>

        <div
          v-if="!workspaceCollapsed"
          class="compare-workspace__divider"
          :class="{ 'compare-workspace__divider--dragging': draggingSplit }"
          @pointerdown="onDividerDown"
        />

        <div v-if="!workspaceCollapsed" class="compare-workspace__right">
          <ProcessPanel
            v-if="panel === 'process'"
            :task="task"
            :evidence="evidence"
            :clause-drafts="clauseDrafts"
            :extracting="extracting"
            :confirming-clauses="confirmingClauses"
            :reparse-doc-ids="reparseDocIds"
            :reparse-all-loading="reparseAllLoading"
            :retrying-pair-ids="retryingPairIds"
            :retrying-compare="retryingCompare"
            @reparse-doc="onReparseDoc"
            @reparse-all="onReparseAll"
            @retry-pair="onRetryPair"
            @retry-compare="onRetryCompare"
            @extract-clauses="onExtractClauses"
            @confirm-clauses="onConfirmClauses"
            @locate="onLocateEvidence"
          />

          <ResultPanel
            v-else-if="panel === 'result'"
            :task="task"
            :overview="overview"
            :evidence="evidence"
            :loading="resultsLoading"
            :exporting="exporting"
            @locate="onLocateEvidence"
            @locate-doc="onLocateDoc"
            @export="exportMenuVisible = true"
            @reparse-doc="onReparseDoc"
            @reparse-all="onReparseAll"
          />

          <FailurePanel
            v-else
            :task="task"
            :reparse-all-loading="reparseAllLoading"
            :retrying-compare="retryingCompare"
            @reparse-doc="onReparseDoc"
            @reparse-all="onReparseAll"
            @retry-compare="onRetryCompare"
            @back="resetToUpload()"
          />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  CompressOutlined,
  DownloadOutlined,
  ExpandOutlined,
  HistoryOutlined,
  PlusOutlined,
  WifiOutlined,
} from '@ant-design/icons-vue'
import UploadPage from './components/UploadPage.vue'
import type { UploadFileItem } from './components/UploadPage.vue'
import PdfWorkspace from './components/PdfWorkspace.vue'
import ProcessPanel from './components/ProcessPanel.vue'
import ResultPanel from './components/ResultPanel.vue'
import FailurePanel from './components/FailurePanel.vue'
import { COMPARE_STATUS_MAP, deriveProjectName, isTerminalStatus } from './constants'
import {
  confirmClauses,
  createTask,
  exportReport,
  extractClauses,
  getEvidence,
  getDocumentFileUrl,
  getExportStatus,
  getOverview,
  getTask,
  getTasks,
  reparseTask,
  retryCompare,
  startParse,
  updateTaskName,
  uploadDocument,
} from '@/api/modules/compare'
import type { ClauseItem, CompareDocMeta, CompareTask, EvidenceItem, TaskOverview } from '@/types'

const route = useRoute()
let disposed = false

/* —— 视图状态 —— */
const view = ref<'upload' | 'workspace'>('upload')
const task = ref<CompareTask | null>(null)
const pendingTaskId = ref<string | null>(null)
const overview = ref<TaskOverview | null>(null)
const evidenceMap = ref(new Map<string, EvidenceItem>())
const evidence = computed(() => [...evidenceMap.value.values()])
const clauseDrafts = ref<ClauseItem[]>([])
const historyTasks = ref<CompareTask[]>([])

/* —— 上传页状态 —— */
const uploadName = ref('')
const uploadItems = ref<UploadFileItem[]>([])
const creating = ref(false)
const uploadError = ref('')

/* —— 动作 loading —— */
const extracting = ref(false)
const confirmingClauses = ref(false)
const reparseDocIds = ref<string[]>([])
const reparseAllLoading = ref(false)
const retryingPairIds = ref<string[]>([])
const retryingCompare = ref(false)
const resultsLoading = ref(false)
const exporting = ref(false)
const exportError = ref('')

/* —— 工作区 UI —— */
const workspaceCollapsed = ref(false)
const splitRatio = ref(0.55)
const draggingSplit = ref(false)
const pairActive = ref<{ docAId: string, docBId: string } | null>(null)
const scanningDocId = ref<string | null>(null)
const connectionLost = ref(false)
const workspaceRef = ref<InstanceType<typeof PdfWorkspace> | null>(null)
const nameDraft = ref('')
const nameSaving = ref(false)
const nameError = ref('')
const suggestedApplied = ref(false)
const lastManualTabAt = ref(0)
const autoFocusedDocIds = ref(new Set<string>())
const exportMenuVisible = ref(false)

const statusInfo = computed(() =>
  task.value ? COMPARE_STATUS_MAP[task.value.status] : { color: 'default', text: '' },
)

const panel = computed<'process' | 'result' | 'failed'>(() => {
  if (!task.value) return 'process'
  if (task.value.status === 'failed') return 'failed'
  if (isTerminalStatus(task.value.status)) return 'result'
  return 'process'
})

/** 文档原文预览 URL 由宿主统一生成（子组件不直接触碰 API 层）。 */
const workspaceDocs = computed<CompareDocMeta[]>(() =>
  task.value?.documents.map((d) => ({
    ...d,
    fileUrl: getDocumentFileUrl(task.value!.id, d.id),
  })) ?? [],
)

const canExport = computed(() =>
  task.value?.status === 'completed' || task.value?.status === 'partial',
)

watch(() => task.value?.name, (name) => {
  if (name != null && !nameSaving.value) nameDraft.value = name
})

onMounted(() => {
  void loadHistory()
  const id = route.query.task
  if (typeof id === 'string' && id) void openTask(id)
})

watch(() => route.query.task, (id) => {
  if (typeof id === 'string' && id && id !== task.value?.id) void openTask(id)
})

onUnmounted(() => {
  disposed = true
  stopPoll()
})

/* —— 轮询：2s，断连 2s→5s→10s 退避，恢复后立即拉全量快照 —— */
const POLL_MS = 2000
const BACKOFF_MS = [2000, 5000, 10000]
let pollTimer: number | null = null
let pollTick = 0
let backoffStep = 0

function stopPoll(): void {
  if (pollTimer !== null) {
    clearTimeout(pollTimer)
    pollTimer = null
  }
}

function startPoll(): void {
  stopPoll()
  pollTick = 0
  backoffStep = 0
  connectionLost.value = false
  schedulePoll(POLL_MS)
}

function schedulePoll(delay: number): void {
  if (disposed) return
  pollTimer = window.setTimeout(() => { void runPoll() }, delay)
}

async function runPoll(): Promise<void> {
  if (!task.value) return
  try {
    const t = await getTask(task.value.id, true)
    task.value = t
    connectionLost.value = false
    backoffStep = 0
    applySuggestedName(t)
    pollTick++

    if (t.status === 'parsing' || t.status === 'comparing' || t.status === 'ai_analyzing') {
      void loadEvidence(true)
      trackPairState(t)
      trackParseFocus(t)
    }

    if (isTerminalStatus(t.status)) {
      stopPoll()
      if (t.status !== 'failed') void loadResults()
      return
    }
    schedulePoll(POLL_MS)
  } catch (err) {
    if (isNotFound(err)) {
      stopPoll()
      resetToUpload('任务不存在，可能已被删除')
      return
    }
    connectionLost.value = true
    const delay = BACKOFF_MS[Math.min(backoffStep, BACKOFF_MS.length - 1)]
    backoffStep++
    schedulePoll(delay)
  }
}

function isNotFound(err: unknown): boolean {
  return (err as { response?: { status?: number } })?.response?.status === 404
}

function applySuggestedName(t: CompareTask): void {
  if (suggestedApplied.value) return
  if (t.nameEditedByUser) {
    suggestedApplied.value = true
    return
  }
  if (t.suggestedName && t.suggestedName !== t.name) {
    task.value = { ...task.value!, name: t.suggestedName, suggestedName: t.suggestedName }
    suggestedApplied.value = true
  }
}

function trackPairState(t: CompareTask): void {
  const processing = t.pairs?.find((p) => p.status === 'processing')
  pairActive.value = processing
    ? { docAId: processing.docAId, docBId: processing.docBId }
    : null
  scanningDocId.value = processing
    ? (pollTick % 2 === 0 ? processing.docAId : processing.docBId)
    : null
}

function trackParseFocus(t: CompareTask): void {
  // 解析完成自动切 Tab；用户最近 10 秒手动切换过则不抢焦点
  const done = t.documents.find((d) => d.parseStatus === 'done' && !autoFocusedDocIds.value.has(d.id))
  if (!done) return
  if (Date.now() - lastManualTabAt.value > 10_000) {
    workspaceRef.value?.focusDoc(done.id)
  }
  autoFocusedDocIds.value.add(done.id)
}

/* —— 证据 / 矩阵 / 结果 —— */
async function loadEvidence(silent = false): Promise<void> {
  if (!task.value) return
  try {
    const list = await getEvidence(task.value.id, silent)
    const map = evidenceMap.value
    for (const ev of list) map.set(ev.id, ev)
    evidenceMap.value = new Map(map)
  } catch {
    /* 证据加载失败不阻塞进度，下一轮重试 */
  }
}

async function loadResults(): Promise<void> {
  if (!task.value || resultsLoading.value) return
  resultsLoading.value = true
  try {
    const [ov, evs] = await Promise.all([getOverview(task.value.id), getEvidence(task.value.id)])
    overview.value = ov
    const map = new Map<string, EvidenceItem>()
    for (const ev of evs) map.set(ev.id, ev)
    evidenceMap.value = map
    clauseDrafts.value = task.value.clauseSnapshot ?? []
  } catch {
    message.error('结果数据加载失败，请稍后重试')
  } finally {
    resultsLoading.value = false
  }
}

function resetWorkspace(): void {
  overview.value = null
  evidenceMap.value = new Map()
  clauseDrafts.value = []
  pairActive.value = null
  scanningDocId.value = null
  suggestedApplied.value = false
  autoFocusedDocIds.value = new Set()
  resultsLoading.value = false
  nameError.value = ''
  exportError.value = ''
}

/* —— 任务加载 / 历史 —— */
async function loadHistory(): Promise<void> {
  try {
    historyTasks.value = await getTasks()
  } catch {
    /* 历史加载失败不阻塞 */
  }
}

async function openTask(id: string): Promise<void> {
  stopPoll()
  resetWorkspace()
  try {
    const t = await getTask(id)
    task.value = t
    pendingTaskId.value = null
    suggestedApplied.value = t.nameEditedByUser === true
    view.value = 'workspace'
    workspaceCollapsed.value = false
    void loadHistory()
    if (isTerminalStatus(t.status)) {
      if (t.status !== 'failed') await loadResults()
    } else {
      startPoll()
    }
  } catch {
    message.error('任务加载失败，请稍后重试')
  }
}

function onHistoryClick({ key }: { key: string }): void {
  if (key !== 'empty') void openTask(key)
}

function resetToUpload(reason = ''): void {
  stopPoll()
  task.value = null
  pendingTaskId.value = null
  uploadItems.value = []
  uploadName.value = ''
  uploadError.value = reason
  creating.value = false
  resetWorkspace()
  view.value = 'upload'
  void loadHistory()
}

/* —— 上传页 —— */
function onAddFiles(files: { file: File, role: 'bid' | 'tender' }[]): void {
  for (const { file, role } of files) {
    const key = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`
    if (!/\.(?:pdf|doc|docx)$/i.test(file.name)) {
      pushUploadItem(key, file, role, '仅支持 PDF / Word 文档')
      continue
    }
    if (file.size > 100 * 1024 * 1024) {
      pushUploadItem(key, file, role, '单份文件不能超过 100MB')
      continue
    }
    if (role === 'tender') {
      uploadItems.value = uploadItems.value.filter((i) => i.role !== 'tender' || i.status === 'error')
      pushUploadItem(key, file, role)
      continue
    }
    const activeBids = uploadItems.value.filter((i) => i.role === 'bid' && i.status !== 'error').length
    if (activeBids >= 5) {
      pushUploadItem(key, file, role, '投标文件最多 5 份')
      continue
    }
    pushUploadItem(key, file, role)
  }
  if (!uploadName.value) {
    uploadName.value = deriveProjectName(uploadItems.value.filter((i) => i.role === 'bid').map((i) => i.name))
  }
}

function pushUploadItem(
  key: string,
  file: File,
  role: 'bid' | 'tender',
  error?: string,
): void {
  uploadItems.value.push({
    key,
    name: file.name,
    size: file.size,
    role,
    status: error ? 'error' : 'pending',
    error,
    file,
  })
}

function setUploadItem(key: string, patch: Partial<UploadFileItem>): void {
  const idx = uploadItems.value.findIndex((i) => i.key === key)
  if (idx >= 0) {
    uploadItems.value = uploadItems.value.map((i, n) => (n === idx ? { ...i, ...patch } : i))
  }
}

async function uploadItemWithProgress(item: UploadFileItem, taskId: string): Promise<void> {
  const startedAt = Date.now()
  setUploadItem(item.key, { status: 'uploading', error: undefined, percent: 0 })
  try {
    const doc = await uploadDocument(taskId, item.file, item.role, (p) => {
      setUploadItem(item.key, { percent: Math.max(2, Math.min(95, p)) })
    })
    // 本地回环传输可能“秒传”，保持上传态至少可见 700ms，避免看起来像瞬间完成
    const elapsed = Date.now() - startedAt
    if (elapsed < 700) {
      await new Promise((r) => setTimeout(r, 700 - elapsed))
    }
    setUploadItem(item.key, { status: 'done', docId: doc.id, percent: 100 })
  } catch {
    setUploadItem(item.key, { status: 'error', error: '上传失败，请重试', percent: undefined })
  }
}

function removeItem(key: string): void {
  uploadItems.value = uploadItems.value.filter((i) => i.key !== key)
  if (!uploadName.value) {
    uploadName.value = deriveProjectName(uploadItems.value.filter((i) => i.role === 'bid').map((i) => i.name))
  }
}

async function retryItem(key: string): Promise<void> {
  const item = uploadItems.value.find((i) => i.key === key)
  if (!item || !pendingTaskId.value) return
  await uploadItemWithProgress(item, pendingTaskId.value)
}

async function handleStart(name: string): Promise<void> {
  if (creating.value) return
  const readyBids = uploadItems.value.filter((i) => i.role === 'bid' && i.status !== 'error').length
  if (readyBids < 2) {
    uploadError.value = '至少需要 2 份投标文件'
    return
  }

  creating.value = true
  uploadError.value = ''
  try {
    let taskId = pendingTaskId.value
    if (!taskId) {
      const t = await createTask(name.trim() || '比标任务')
      taskId = t.id
      pendingTaskId.value = taskId
      task.value = t
    }

    const pending = uploadItems.value.filter((i) => i.status === 'pending' || i.status === 'error')
    await Promise.allSettled(pending.map(async (item) => uploadItemWithProgress(item, taskId!)))
    // v2 修订：上传完成后统一批量并发解析，不再逐份入队
    await startParse(taskId!)

    const successBids = uploadItems.value.filter((i) => i.role === 'bid' && i.status === 'done').length
    if (successBids >= 2) {
      const t = await getTask(taskId)
      task.value = t
      resetWorkspace()
      view.value = 'workspace'
      startPoll()
    } else {
      uploadError.value = '可用投标文件不足 2 份，请重试失败文件'
    }
  } catch {
    uploadError.value = '任务创建失败，请重试'
  } finally {
    creating.value = false
  }
}

/* —— 重试 / 条款 / 比对 —— */
async function onReparseDoc(docId: string): Promise<void> {
  if (!task.value || reparseDocIds.value.includes(docId)) return
  reparseDocIds.value = [...reparseDocIds.value, docId]
  try {
    task.value = await reparseTask(task.value.id, [docId])
    startPoll()
  } catch {
    message.error('重新解析提交失败，请稍后重试')
  } finally {
    reparseDocIds.value = reparseDocIds.value.filter((id) => id !== docId)
  }
}

async function onReparseAll(): Promise<void> {
  if (!task.value || reparseAllLoading.value) return
  reparseAllLoading.value = true
  try {
    task.value = await reparseTask(task.value.id)
    startPoll()
  } catch {
    message.error('重新解析提交失败，请稍后重试')
  } finally {
    reparseAllLoading.value = false
  }
}

async function onRetryPair(pairId: string): Promise<void> {
  if (!task.value || retryingPairIds.value.includes(pairId)) return
  retryingPairIds.value = [...retryingPairIds.value, pairId]
  try {
    task.value = await retryCompare(task.value.id, [pairId])
    startPoll()
  } catch {
    message.error('该比对对重试失败，请稍后再试')
  } finally {
    retryingPairIds.value = retryingPairIds.value.filter((id) => id !== pairId)
  }
}

async function onRetryCompare(): Promise<void> {
  if (!task.value || retryingCompare.value) return
  retryingCompare.value = true
  try {
    task.value = await retryCompare(task.value.id)
    startPoll()
  } catch {
    message.error('重新对比提交失败，请稍后再试')
  } finally {
    retryingCompare.value = false
  }
}

async function onExtractClauses(): Promise<void> {
  if (!task.value || extracting.value) return
  extracting.value = true
  try {
    clauseDrafts.value = await extractClauses(task.value.id)
  } catch {
    message.error('条款提取失败，请稍后重试')
  } finally {
    extracting.value = false
  }
}

async function onConfirmClauses(list: ClauseItem[]): Promise<void> {
  if (!task.value || confirmingClauses.value) return
  confirmingClauses.value = true
  try {
    task.value = await confirmClauses({
      taskId: task.value.id,
      clauses: list.map((c) => ({
        clauseId: c.id,
        title: c.title,
        content: c.content || c.title,
        mandatory: c.mandatory,
      })),
    })
    clauseDrafts.value = []
    startPoll()
  } catch {
    message.error('条款确认失败，请稍后重试')
  } finally {
    confirmingClauses.value = false
  }
}

/* —— 项目名 —— */
async function saveName(): Promise<void> {
  if (!task.value || nameSaving.value) return
  const next = nameDraft.value.trim()
  if (!next || next === task.value.name) return
  nameSaving.value = true
  nameError.value = ''
  try {
    task.value = await updateTaskName(task.value.id, next)
    suggestedApplied.value = true
  } catch {
    nameError.value = '名称保存失败，可重试'
  } finally {
    nameSaving.value = false
  }
}

/* —— 溯源 / 导出 —— */
function onLocateEvidence(ev: EvidenceItem): void {
  workspaceRef.value?.locate(ev)
}

function onLocateDoc(payload: { docId: string, page: number }): void {
  workspaceRef.value?.locateDoc(payload.docId, payload.page)
}

async function handleExport(format: 'docx' | 'pdf'): Promise<void> {
  if (!task.value || exporting.value) return
  exporting.value = true
  exportError.value = ''
  exportMenuVisible.value = false
  message.loading({ content: '报告生成中…', key: 'compare-export', duration: 0 })
  try {
    const job = await exportReport(task.value.id, format)
    pollExport(task.value.id, job.exportId)
  } catch {
    exporting.value = false
    exportError.value = '导出请求失败，可重试'
    message.error({ content: '导出请求失败', key: 'compare-export' })
  }
}

function onExportMenuClick({ key }: { key: string }): void {
  if (key === 'docx' || key === 'pdf') void handleExport(key)
}

function pollExport(taskId: string, exportId: string): void {
  const timer = window.setInterval(async () => {
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
        exportError.value = '导出失败，可重试'
        message.error({ content: '导出失败，可重试', key: 'compare-export' })
      }
    } catch {
      clearInterval(timer)
      exporting.value = false
      exportError.value = '导出状态查询失败，可重试'
      message.error({ content: '导出状态查询失败', key: 'compare-export' })
    }
  }, 1500)
}

/* —— 分栏拖拽 —— */
function onDividerDown(e: PointerEvent): void {
  e.preventDefault()
  draggingSplit.value = true
  const onMove = (ev: PointerEvent): void => {
    const rect = (e.currentTarget as HTMLElement).parentElement?.getBoundingClientRect()
    if (!rect) return
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
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.compare-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.compare-workspace {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.compare-workspace__bar {
  display: flex;
  align-items: center;
  gap: @spacing-md;
  padding: 0 0 @spacing-md;
  flex-shrink: 0;
}

.compare-workspace__name {
  flex: 1;
  min-width: 0;
  display: flex;
  align-items: center;
  gap: @spacing-sm;
}

.compare-workspace__name-input {
  max-width: 360px;
}

.compare-workspace__name-error {
  font-size: @font-size-xs;
  color: @danger;
  white-space: nowrap;
}

.compare-workspace__history {
  max-width: 320px;

  &-name {
    display: inline-block;
    max-width: 180px;
    overflow: hidden;
    text-overflow: ellipsis;
    vertical-align: middle;
  }
}

.compare-workspace__export-error {
  font-size: @font-size-xs;
  color: @danger;
  white-space: nowrap;
}

.compare-workspace__banner {
  display: flex;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-xs @spacing-md;
  margin-bottom: @spacing-sm;
  border: 1px solid @warning;
  border-radius: @radius-base;
  background: color-mix(in srgb, @warning 8%, @card-bg);
  font-size: @font-size-xs;
  color: @warning;
  flex-shrink: 0;
}

.compare-workspace__split {
  flex: 1;
  min-height: 0;
  display: flex;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-lg;
  overflow: hidden;
}

.compare-workspace__left {
  flex-shrink: 0;
  min-width: 0;
  padding: @spacing-md;
  display: flex;
  flex-direction: column;
}

.compare-workspace__divider {
  flex-shrink: 0;
  width: 3px;
  cursor: col-resize;
  position: relative;

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

.compare-workspace__right {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
}
</style>
