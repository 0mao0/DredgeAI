<template>
  <div class="compare-page">
    <!-- 上传引导页 -->
    <UploadPage
      v-if="view === 'upload'"
      :items="uploadItems"
      :creating="creating"

      :upload-error="uploadError"
      @add-files="onAddFiles"
      @remove="removeItem"
      @retry="retryItem"
      @start="handleStart"
    />

    <!-- 续传面板：点了「开始分析」但文件还没传完，先进入工作区继续上传 -->
    <div v-else-if="!task" class="compare-uploading">
      <div class="compare-uploading__head">
        <div class="compare-uploading__title">
          <LoadingOutlined v-if="creating" class="compare-uploading__spin" />
          <UploadOutlined v-else />
          <span>{{ creating ? '上传完成，正在创建任务并开始解析…' : `正在上传文件 ${uploadedCount}/${uploadItems.length}` }}</span>
        </div>
        <AppButton size="sm" :disabled="creating" @click="backToUpload()">返回上传页</AppButton>
      </div>

      <div v-if="finalizeError" class="compare-uploading__error">
        <a-alert type="error" :message="finalizeError" show-icon />
        <AppButton size="sm" variant="primary" @click="finalizeTask()">重试</AppButton>
      </div>

      <div class="compare-uploading__list">
        <UploadFileRow
          v-for="item in uploadItems"
          :key="item.key"
          :item="item"
          :disabled="creating"
          @retry="retryItem(item.key)"
          @remove="removeItem(item.key)"
        />
      </div>
    </div>

    <!-- 工作区：analyzing / result / failed 共用同一左右分栏 -->
    <div v-else-if="task" class="compare-workspace">
      <div class="compare-workspace__bar">
        <div
          class="compare-workspace__name"
          @mouseleave="cancelEditName"
        >
          <template v-if="projectNameVisible">
            <span
              v-if="!editingName"
              class="compare-workspace__name-title"
              title="悬停编辑项目名"
              @mouseenter="startEditName"
            >{{ task.name }}</span>
            <template v-else>
              <a-input
                v-model:value="nameDraft"
                :maxlength="128"
                class="compare-workspace__name-input"
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
              <span v-if="nameError" class="compare-workspace__name-error">{{ nameError }}</span>
            </template>
          </template>
        </div>

        <!--
          trigger="click"
          placement="bottomRight"
          :open="exportMenuVisible"
          @open-change="exportMenuVisible = $event"
        >
          <AppButton
            size="sm"
            variant="primary"
            :disabled="!canExport"
            :loading="exporting"
            @click.prevent
          >
            <DownloadOutlined />导出
          </AppButton>
          <template #overlay>
            <a-menu @click="onExportMenuClick">
              <a-menu-item key="docx">Word 报告（.docx）</a-menu-item>
              <a-menu-item key="pdf">PDF 报告</a-menu-item>
            </a-menu>
          </template>
        </a-dropdown>
        -->

        <AppButton size="sm" variant="text" @click="openDrawer()">
          <EyeOutlined />文档预览
        </AppButton>

        <span class="compare-workspace__new-task" @click="resetToUpload()">新建任务</span>
      </div>

      <div v-if="connectionLost" class="compare-workspace__banner">
        <WifiOutlined />连接中断，正在重试…
      </div>

      <div class="compare-workspace__split">
        <div class="compare-workspace__right">
          <ProcessPanel
            :task="task"
            :overview="overview"
            :evidence="evidence"
            :clause-drafts="clauseDrafts"
            :extracting="extracting"
            :confirming-clauses="confirmingClauses"
            :reparse-doc-ids="reparseDocIds"
            :reparse-all-loading="reparseAllLoading"
            :retrying-pair-ids="retryingPairIds"
            :retrying-compare="retryingCompare"
            :retrying-ai="retryingAi"
            @reparse-doc="onReparseDoc"
            @reparse-all="onReparseAll"
            @retry-pair="onRetryPair"
            @retry-compare="onRetryCompare"
            @retry-ai="onRetryAi"
            @extract-clauses="onExtractClauses"
            @confirm-clauses="onConfirmClauses"
            @locate="onLocateEvidence"
            @locate-refs="onLocateRefs"
          />

          <div class="compare-workspace__export-footer">
            <a-dropdown
              trigger="click"
              placement="topRight"
              :open="exportMenuVisible"
              @open-change="exportMenuVisible = $event"
            >
              <AppButton
                size="sm"
                variant="primary"
                :disabled="!canExport"
                :loading="exporting"
                @click.prevent
              >
                <DownloadOutlined />导出报告
              </AppButton>
              <template #overlay>
                <a-menu @click="onExportMenuClick">
                  <a-menu-item key="docx">Word 报告（.docx）</a-menu-item>
                  <a-menu-item key="pdf">PDF 报告</a-menu-item>
                </a-menu>
              </template>
            </a-dropdown>

            <span v-if="exportError" class="compare-workspace__export-error">{{ exportError }}</span>
          </div>
        </div>
      </div>

      <a-drawer
        v-model:open="drawerOpen"
        title="PDF 溯源"
        placement="right"
        width="78vw"
        :body-style="{ padding: 0, height: '100%' }"
        :closable="true"
      >
        <div v-if="drawerMounted" class="compare-drawer-host">
          <PdfWorkspace
            ref="workspaceRef"
            :documents="workspaceDocs"
            :pair-active="pairActive"
            :scanning-doc-id="scanningDocId"
          />
        </div>
      </a-drawer>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue'
import { AppButton } from '@shared/web'
import { useRoute } from 'vue-router'
import { message } from 'ant-design-vue'
import {
  DownloadOutlined,
  EyeOutlined,
  LoadingOutlined,
  UploadOutlined,
  WifiOutlined,
} from '@ant-design/icons-vue'
import UploadPage from './components/UploadPage.vue'
import type { UploadFileItem } from './components/UploadPage.vue'
import UploadFileRow from './components/UploadFileRow.vue'
import PdfWorkspace from './components/PdfWorkspace.vue'
import ProcessPanel from './components/ProcessPanel.vue'
import { MAX_BID_DOCUMENTS, formatProjectName, isTerminalStatus } from './constants'
import {
  confirmClauses,
  createTask,
  deleteDraft,
  deleteDraftDocument,
  exportReport,
  extractClauses,
  assembleOverview,
  getDocuments,
  getEvidence,
  getDocumentFileUrl,
  getExportStatus,
  getMatrix,
  getTask,
  getTasks,
  reparseTask,
  retryCompare,
  retryAiAnalysis,
  startParse,
  uploadDraftDocument,
  updateTaskName,
} from '@/api/modules/compare'
import type { BlockRange, ClauseItem, CompareDocMeta, CompareTask, EvidenceItem, TaskOverview } from '@/types'

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
const uploadItems = ref<UploadFileItem[]>([])
const creating = ref(false)
const uploadError = ref('')
const draftId = ref(newDraftId())
const startRequested = ref(false)
const finalizeError = ref('')

/* —— 动作 loading —— */
const extracting = ref(false)
const confirmingClauses = ref(false)
const reparseDocIds = ref<string[]>([])
const reparseAllLoading = ref(false)
const retryingPairIds = ref<string[]>([])
const retryingCompare = ref(false)
const retryingAi = ref(false)
const resultsLoading = ref(false)
const exporting = ref(false)
const exportError = ref('')

/* —— 工作区 UI —— */
const drawerOpen = ref(false)
const drawerMounted = ref(false)
const pairActive = ref<{ docAId: string, docBId: string } | null>(null)
const scanningDocId = ref<string | null>(null)
const connectionLost = ref(false)
const workspaceRef = ref<InstanceType<typeof PdfWorkspace> | null>(null)
const nameDraft = ref('')
const nameDraftTouched = ref(false)
const editingName = ref(false)
const nameSaving = ref(false)
const nameError = ref('')
const suggestedApplied = ref(false)
const exportMenuVisible = ref(false)

const uploadedCount = computed(() => uploadItems.value.filter((i) => i.status === 'done').length)
const allUploadsSettled = computed(() =>
  uploadItems.value.length > 0
  && uploadItems.value.every((i) => i.status !== 'uploading' && i.status !== 'pending'),
)

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

const bidDocCount = computed(() =>
  task.value?.documents.filter((d) => d.role === 'bid').length ?? 0,
)

const projectNameVisible = computed(() => {
  const t = task.value
  if (!t) return false
  return t.nameEditedByUser === true || (t.suggestedName != null && t.suggestedName.trim() !== '')
})

const suggestedDisplayName = computed(() => {
  const t = task.value
  if (!t) return ''
  if (t.nameEditedByUser) return t.name
  return t.suggestedName ? formatProjectName(t.suggestedName, bidDocCount.value) : ''
})

const canConfirmName = computed(() => {
  const t = task.value
  if (!t) return false
  const next = nameDraft.value.trim()
  return next !== '' && next !== t.name
})

function syncNameDraft(): void {
  if (nameSaving.value || nameDraftTouched.value) return
  nameDraft.value = suggestedDisplayName.value
}

watch(
  [() => task.value?.name, () => task.value?.suggestedName, () => task.value?.nameEditedByUser],
  syncNameDraft,
)

watch(allUploadsSettled, (settled) => {
  if (settled && startRequested.value) void maybeFinalize()
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
  if (t.suggestedName && t.suggestedName.trim()) {
    const bidCount = t.documents.filter((d) => d.role === 'bid').length
    if (!nameDraftTouched.value) nameDraft.value = formatProjectName(t.suggestedName, bidCount)
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
    const id = task.value.id
    // 一次并行拉取矩阵/证据/文档并组装，避免 getOverview + getEvidence 双重请求同一批证据
    const [matrix, evs, docs] = await Promise.all([getMatrix(id), getEvidence(id), getDocuments(id)])
    overview.value = assembleOverview(matrix, evs, docs)
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
  drawerOpen.value = false
  // 任务加载即挂载抽屉内容：PDF 后台预加载，点击“查看”时直接定位，避免首次点击才加载导致跳页丢失
  drawerMounted.value = true
  resultsLoading.value = false
  nameDraft.value = ''
  nameDraftTouched.value = false
  nameSaving.value = false
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
    nameDraftTouched.value = false
    nameDraft.value = t.nameEditedByUser
      ? t.name
      : (t.suggestedName
          ? formatProjectName(t.suggestedName, t.documents.filter((d) => d.role === 'bid').length)
          : '')
    view.value = 'workspace'
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

function resetToUpload(reason = ''): void {
  stopPoll()
  if (draftId.value) void deleteDraft(draftId.value).catch(() => {})
  draftId.value = newDraftId()
  task.value = null
  pendingTaskId.value = null
  uploadItems.value = []
  uploadError.value = reason
  creating.value = false
  startRequested.value = false
  finalizeError.value = ''
  resetWorkspace()
  view.value = 'upload'
  void loadHistory()
}

/** 上传续传面板：返回上传页但保留已上传文件和会话，可继续增删文件。 */
function backToUpload(): void {
  view.value = 'upload'
}

/* —— 上传页：选中即上传到会话（不建任务），点「开始分析」才建任务/解析 —— */

function newDraftId(): string {
  return typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`
}

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
    if (activeBids >= MAX_BID_DOCUMENTS) {
      pushUploadItem(key, file, role, `投标文件最多 ${MAX_BID_DOCUMENTS} 份`)
      continue
    }
    pushUploadItem(key, file, role)
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
    status: error ? 'error' : 'uploading',
    error,
    file,
  })
  if (!error) void uploadItem(key)
}

function setUploadItem(key: string, patch: Partial<UploadFileItem>): void {
  const idx = uploadItems.value.findIndex((i) => i.key === key)
  if (idx >= 0) {
    uploadItems.value = uploadItems.value.map((i, n) => (n === idx ? { ...i, ...patch } : i))
  }
}

async function uploadItem(key: string): Promise<void> {
  const item = uploadItems.value.find((i) => i.key === key)
  if (!item) return
  const startedAt = Date.now()
  setUploadItem(key, { status: 'uploading', error: undefined, percent: 0, startedAt })
  try {
    const doc = await uploadDraftDocument(draftId.value, item.file, item.role, (p) => {
      setUploadItem(key, { percent: Math.max(2, Math.min(95, p)) })
    })
    // 本地回环传输可能“秒传”，保持上传态至少可见 300ms，避免看起来像瞬间完成
    const elapsed = Date.now() - startedAt
    if (elapsed < 300) {
      await new Promise((r) => setTimeout(r, 300 - elapsed))
    }
    setUploadItem(key, { status: 'done', docId: doc.id, percent: 100 })
  } catch {
    setUploadItem(key, { status: 'error', error: '上传失败，请重试', percent: undefined })
  }
}

function removeItem(key: string): void {
  const item = uploadItems.value.find((i) => i.key === key)
  if (item?.status === 'done' && item.docId) {
    void deleteDraftDocument(draftId.value, item.docId).catch(() => {})
  }
  uploadItems.value = uploadItems.value.filter((i) => i.key !== key)
}

async function retryItem(key: string): Promise<void> {
  await uploadItem(key)
}

function handleStart(): void {
  if (creating.value) return
  const readyBids = uploadItems.value.filter((i) => i.role === 'bid' && i.status !== 'error').length
  if (readyBids < 2) {
    uploadError.value = '至少需要 2 份投标文件'
    return
  }

  uploadError.value = ''
  startRequested.value = true
  view.value = 'workspace'
  maybeFinalize()
}

function maybeFinalize(): void {
  if (!startRequested.value || creating.value || task.value || !allUploadsSettled.value) return
  void finalizeTask()
}

async function finalizeTask(): Promise<void> {
  if (creating.value || task.value) return
  const okBids = uploadItems.value.filter((i) => i.role === 'bid' && i.status === 'done').length
  if (okBids < 2) {
    finalizeError.value = '可用投标文件不足 2 份，请返回上传页重试'
    return
  }

  creating.value = true
  finalizeError.value = ''
  try {
    const t = await createTask('比标任务', draftId.value)
    const started = await startParse(t.id, true)
    task.value = started
    pendingTaskId.value = null
    uploadItems.value = []
    startRequested.value = false
    resetWorkspace()
    view.value = 'workspace'
    startPoll()
  } catch {
    finalizeError.value = '任务创建失败，请重试'
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
  // 重新对比会重跑算法并重写证据：先清空旧结果，避免比对过程中还挂着上一轮的图/证据
  overview.value = null
  evidenceMap.value = new Map()
  try {
    task.value = await retryCompare(task.value.id)
    startPoll()
  } catch {
    message.error('重新对比提交失败，请稍后再试')
  } finally {
    retryingCompare.value = false
  }
}

async function onRetryAi(): Promise<void> {
  if (!task.value || retryingAi.value) return
  retryingAi.value = true
  // 重新抽取会删除并重建 AI 证据：先清空旧证据，避免分析过程中残留上一轮内容
  evidenceMap.value = new Map()
  try {
    task.value = await retryAiAnalysis(task.value.id)
    startPoll()
  } catch {
    message.error('重新抽取提交失败，请稍后再试')
  } finally {
    retryingAi.value = false
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

/* —— 项目名（悬停进入编辑态，离开恢复标题） —— */
function startEditName(): void {
  if (!task.value || editingName.value) return
  nameDraft.value = task.value.name
  nameDraftTouched.value = false
  nameError.value = ''
  editingName.value = true
  nextTick(() => {
    document.querySelector<HTMLInputElement>('.compare-workspace__name-input input')?.focus()
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
    task.value = await updateTaskName(task.value.id, next)
    suggestedApplied.value = true
    nameDraftTouched.value = false
    nameDraft.value = next
    editingName.value = false
  } catch {
    nameError.value = '名称保存失败，可重试'
  } finally {
    nameSaving.value = false
  }
}

/* —— 溯源：打开抽屉并定位（抽屉内容首次打开后常驻，PDF 只加载一次） —— */
type PendingLocate
  = | { kind: 'evidence', ev: EvidenceItem }
    | { kind: 'refs', refs: BlockRange[] }

const pendingLocate = ref<PendingLocate | null>(null)

function openDrawer(): void {
  drawerMounted.value = true
  drawerOpen.value = true
}

function applyPendingLocate(): void {
  const p = pendingLocate.value
  if (!p || !workspaceRef.value) return
  pendingLocate.value = null
  if (p.kind === 'evidence') {
    workspaceRef.value.locate(p.ev)
  } else {
    workspaceRef.value.locateRefs(p.refs)
  }
}

function locateInDrawer(p: PendingLocate): void {
  pendingLocate.value = p
  const wasMounted = drawerMounted.value
  drawerMounted.value = true
  drawerOpen.value = true
  if (workspaceRef.value) {
    applyPendingLocate()
  } else if (!wasMounted) {
    nextTick(applyPendingLocate)
  }
}

function onLocateEvidence(ev: EvidenceItem): void {
  locateInDrawer({ kind: 'evidence', ev })
}

function onLocateRefs(refs: BlockRange[]): void {
  locateInDrawer({ kind: 'refs', refs })
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
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.compare-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.compare-uploading {
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: @spacing-md;
  padding: @spacing-lg 0;
  max-width: 720px;
  margin: 0 auto;
  width: 100%;

  &__head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: @spacing-md;
    flex-wrap: wrap;
  }

  &__title {
    display: flex;
    align-items: center;
    gap: @spacing-sm;
    font-size: @font-size-base;
    font-weight: @font-weight-medium;
    color: @text-primary;
  }

  &__spin {
    color: @brand-primary;
  }

  &__error {
    display: flex;
    align-items: center;
    gap: @spacing-md;
    flex-wrap: wrap;
  }

  &__list {
    display: flex;
    flex-direction: column;
    gap: @spacing-xs;
  }
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

.compare-workspace__name-title {
  flex: 0 1 auto;
  width: fit-content;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: @font-size-lg;
  font-weight: @font-weight-bold;
  color: @text-primary;
  cursor: text;
}

.compare-workspace__name-input {
  flex: 0 1 auto;
  width: auto;
  min-width: 160px;
  max-width: 100%;
  font-size: @font-size-lg;
  font-weight: @font-weight-semibold;
  field-sizing: content;
}

.compare-workspace__name-error {
  font-size: @font-size-xs;
  color: @danger;
  white-space: nowrap;
}

.compare-workspace__new-task {
  flex-shrink: 0;
  font-size: @font-size-sm;
  color: @text-secondary;
  cursor: pointer;
  user-select: none;
  transition: color @transition-fast;

  &:hover {
    color: @brand-primary;
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
  flex-direction: column;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-lg;
  overflow: hidden;
}

.compare-workspace__right {
  flex: 1;
  min-width: 0;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.compare-workspace__right > .process-panel {
  flex: 1;
  min-height: 0;
  height: auto;
}

.compare-workspace__export-footer {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: @spacing-sm;
  padding: @spacing-md @spacing-base @spacing-base;
  border-top: 1px solid @border-color;
}

.compare-drawer-host {
  height: 100%;
  min-height: 0;
}
</style>
