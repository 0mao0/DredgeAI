<template>
  <div class="compare-page">
    <!-- 上传引导页 -->
    <template v-if="view === 'upload'">
      <div v-if="readTasks.length" class="compare-read-select">
        <span class="compare-read-select__label">读标基准库（可选）</span>
        <a-select
          v-model:value="selectedReadTaskId"
          allow-clear
          placeholder="选择已 Ready 的读标任务，自动生成条款快照"
          class="compare-read-select__select"
        >
          <a-select-option v-for="t in readTasks" :key="t.id" :value="t.id">
            {{ t.name }}（v{{ t.baselineVersion }}）
          </a-select-option>
        </a-select>
      </div>
      <UploadPage
        :items="uploadItems"
        :creating="creating"

        :upload-error="uploadError"
        @add-files="onAddFiles"
        @remove="removeItem"
        @retry="retryItem"
        @start="handleStart"
      />
    </template>

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
                ref="nameInputRef"
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

        <a-tooltip title="文档预览">
          <AppButton size="sm" variant="text" @click="openDrawer()">
            <EyeOutlined />
          </AppButton>
        </a-tooltip>

        <a-tooltip title="新建任务">
          <AppButton size="sm" variant="text" @click="handleNewTask">
            <PlusOutlined />
          </AppButton>
        </a-tooltip>
      </div>

      <div v-if="connectionLost" class="compare-workspace__banner">
        <WifiOutlined />连接中断，正在重试…
      </div>

      <div v-if="resultsError && !resultsLoading" class="compare-workspace__banner">
        结果数据加载失败
        <AppButton size="sm" @click="loadResults">重试</AppButton>
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
            :ir-epoch="irEpoch"
            @reparse-doc="requestReparseDoc"
            @reparse-all="requestReparseAll"
            @retry-pair="onRetryPair"
            @retry-compare="requestRetryCompare"
            @retry-ai="requestRetryAi"
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
import { useRoute, useRouter } from 'vue-router'
import { message, Modal } from 'ant-design-vue'
import {
  DownloadOutlined,
  EyeOutlined,
  PlusOutlined,
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
import { describeUploadError, detectFileTypeWarning } from './uploadErrors'
import {
  confirmClauses,
  createTask,
  deleteDraft,
  deleteDraftDocument,
  exportReport,
  triggerExtractClauses,
  assembleOverview,
  getDocuments,
  getEvidence,
  getDocumentFileUrl,
  getExportStatus,
  getMatrix,
  getTask,
  reparseTask,
  retryCompare,
  retryAiAnalysis,
  startParse,
  uploadDraftDocument,
  updateTaskName,
} from '@/api/modules/compare'
import { getTenderReadTasks } from '@/api/modules/tenderRead'
import type { BlockRange, ClauseItem, CompareDocMeta, CompareTask, EvidenceItem, TaskOverview, TenderReadingTask } from '@/types'

const route = useRoute()
const router = useRouter()
let disposed = false

/* —— 视图状态 —— */
const view = ref<'upload' | 'workspace'>('upload')
const task = ref<CompareTask | null>(null)
const pendingTaskId = ref<string | null>(null)
const overview = ref<TaskOverview | null>(null)
const evidenceMap = ref(new Map<string, EvidenceItem>())
const evidence = computed(() => [...evidenceMap.value.values()])
const clauseDrafts = ref<ClauseItem[]>([])

/* —— 上传页状态 —— */
const uploadItems = ref<UploadFileItem[]>([])
const creating = ref(false)
const uploadError = ref('')
const draftId = ref(newDraftId())
const startRequested = ref(false)
const finalizeError = ref('')
const readTasks = ref<TenderReadingTask[]>([])
const selectedReadTaskId = ref('')

/* —— 动作 loading —— */
const extracting = ref(false)
const confirmingClauses = ref(false)
const reparseDocIds = ref<string[]>([])
const reparseAllLoading = ref(false)
const retryingPairIds = ref<string[]>([])
const retryingCompare = ref(false)
const retryingAi = ref(false)
const resultsLoading = ref(false)
const resultsError = ref(false)
const exporting = ref(false)
const exportError = ref('')
/** IR 代际：重解析/重新比对会使旧 IR 坐标失效，自增以通知子组件清缓存 */
const irEpoch = ref(0)

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
  void loadReadTasks()
  const id = route.query.task
  if (typeof id === 'string' && id) void openTask(id)
})

watch(() => route.query.task, (id) => {
  if (typeof id === 'string' && id && id !== task.value?.id) void openTask(id)
})

onUnmounted(() => {
  disposed = true
  stopPoll()
  clearExportPoll()
  message.destroy('compare-export')
})

/* —— 轮询：2s，断连 2s→5s→10s 退避，恢复后立即拉全量快照；代际令牌防止在途轮询写回脏数据 —— */
const POLL_MS = 2000
const BACKOFF_MS = [2000, 5000, 10000]
let pollTimer: number | null = null
let pollGen = 0
let pollTick = 0
let backoffStep = 0

function stopPoll(): void {
  pollGen++
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
  if (pollTimer !== null) clearTimeout(pollTimer)
  pollTimer = window.setTimeout(() => {
    pollTimer = null
    void runPoll()
  }, delay)
}

async function runPoll(): Promise<void> {
  const gen = pollGen
  const id = task.value?.id
  if (!id) return
  const stale = (): boolean => disposed || gen !== pollGen || task.value?.id !== id
  try {
    const t = await getTask(id, true)
    if (stale()) return
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
    // 等待条款确认期间状态只随用户动作变化，停止轮询（确认/重解析会重新 startPoll）
    if (t.progress.stage === 'clauses') {
      if (t.clauseDrafts?.length) {
        clauseDrafts.value = t.clauseDrafts
      }
      extracting.value = false
      stopPoll()
      return
    }
    // 条款提取失败：停轮询并提示，用户可再次触发提取
    if (t.progress.stage === 'clauses_extract_failed') {
      extracting.value = false
      stopPoll()
      message.error(t.progress.message || '条款提取失败，请重试')
      return
    }
    schedulePoll(POLL_MS)
  } catch (err) {
    if (stale()) return
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
  const id = task.value?.id
  if (!id) return
  try {
    const list = await getEvidence(id, silent)
    if (disposed || task.value?.id !== id) return
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
    if (disposed || task.value?.id !== id) return
    overview.value = assembleOverview(matrix, evs, docs)
    const map = new Map<string, EvidenceItem>()
    for (const ev of evs) map.set(ev.id, ev)
    evidenceMap.value = map
    clauseDrafts.value = task.value.clauseSnapshot ?? []
    resultsError.value = false
  } catch {
    resultsError.value = true
    message.error('结果数据加载失败，请稍后重试')
  } finally {
    resultsLoading.value = false
  }
}

function resetWorkspace(): void {
  clearExportPoll()
  exporting.value = false
  message.destroy('compare-export')
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
  resultsError.value = false
}

/* —— 任务加载 / 基准库 —— */
async function loadReadTasks(): Promise<void> {
  try {
    readTasks.value = (await getTenderReadTasks()).filter((t) => t.status === 'ready')
  } catch {
    /* 读标基准库加载失败不阻塞比标上传 */
  }
}

async function openTask(id: string): Promise<void> {
  stopPoll()
  resetWorkspace()
  const gen = pollGen
  try {
    const t = await getTask(id)
    if (disposed || gen !== pollGen) return
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
  if (route.query.task) void router.replace({ query: { ...route.query, task: undefined } })
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
}

/** 新建任务：先确认再放弃当前任务回到上传页 */
function handleNewTask(): void {
  Modal.confirm({
    title: '新建任务',
    content: '确定要放弃当前任务并新建一个比标任务吗？',
    okText: '确定',
    cancelText: '取消',
    onOk() {
      resetToUpload()
    },
  })
}

/** 上传续传面板：返回上传页但保留已上传文件和会话，可继续增删文件。 */
function backToUpload(): void {
  // 复位后开始分析的自动触发，回到上传页后需重新点击「开始分析」
  startRequested.value = false
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
      // 替换招标文件前先把旧文件从服务端 draft 会话删掉，避免 createTask 取到残留文档
      for (const old of uploadItems.value) {
        if (old.role === 'tender' && old.status === 'done' && old.docId) {
          void deleteDraftDocument(draftId.value, old.docId).catch(() => {})
        }
      }
      uploadItems.value = uploadItems.value.filter((i) => i.role !== 'tender')
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
    // 先本地识别文件头：格式与扩展名不符只提示、不拦截（解析链路按内容识别格式）
    const localWarning = await detectFileTypeWarning(item.file)
    const doc = await uploadDraftDocument(draftId.value, item.file, item.role, (p) => {
      setUploadItem(key, { percent: Math.max(2, Math.min(95, p)) })
    })
    // 本地回环传输可能“秒传”，保持上传态至少可见 300ms，避免看起来像瞬间完成
    const elapsed = Date.now() - startedAt
    if (elapsed < 300) {
      await new Promise((r) => setTimeout(r, 300 - elapsed))
    }
    setUploadItem(key, { status: 'done', docId: doc.id, percent: 100, warning: localWarning ?? undefined })
  } catch (err) {
    setUploadItem(key, { status: 'error', error: describeUploadError(err), percent: undefined })
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
  if (!pendingTaskId.value) {
    const okBids = uploadItems.value.filter((i) => i.role === 'bid' && i.status === 'done').length
    if (okBids < 2) {
      finalizeError.value = '可用投标文件不足 2 份，请返回上传页重试'
      return
    }
  }

  creating.value = true
  finalizeError.value = ''
  try {
    // 阶段一：建任务（draftId 一经消费即失效，成功后记录 pendingTaskId 供重试复用）
    if (!pendingTaskId.value) {
      try {
        const created = await createTask('比标任务', draftId.value, selectedReadTaskId.value || undefined)
        pendingTaskId.value = created.id
      } catch {
        finalizeError.value = '任务创建失败，请重试'
        return
      }
    }
    // 阶段二：启动解析。失败时保留 pendingTaskId，重试只对既有任务重启解析，避免重复建任务
    let started: CompareTask
    try {
      started = await startParse(pendingTaskId.value, true)
    } catch {
      finalizeError.value = '任务已创建，但解析启动失败，请点击重试'
      return
    }
    task.value = started
    pendingTaskId.value = null
    uploadItems.value = []
    startRequested.value = false
    resetWorkspace()
    view.value = 'workspace'
    void router.replace({ query: { ...route.query, task: started.id } })
    startPoll()
  } finally {
    creating.value = false
  }
}

/* —— 重试 / 条款 / 比对 —— */

/** 破坏性/长耗时操作统一二次确认 */
function confirmAction(title: string, content: string, action: () => void): void {
  Modal.confirm({ title, content, okText: '确定', cancelText: '取消', onOk: () => action() })
}

function requestReparseDoc(docId: string): void {
  confirmAction('重新解析', '将重新解析该文档并清除其旧证据坐标，确定继续吗？', () => void onReparseDoc(docId))
}

async function onReparseDoc(docId: string): Promise<void> {
  if (!task.value || reparseDocIds.value.includes(docId)) return
  reparseDocIds.value = [...reparseDocIds.value, docId]
  // 重解析会重建 IR：先清空旧证据/结果与 IR 代际缓存，避免展示指向失效坐标的旧数据
  overview.value = null
  evidenceMap.value = new Map()
  irEpoch.value++
  try {
    task.value = await reparseTask(task.value.id, [docId])
    startPoll()
  } catch {
    message.error('重新解析提交失败，请稍后重试')
  } finally {
    reparseDocIds.value = reparseDocIds.value.filter((id) => id !== docId)
  }
}

function requestReparseAll(): void {
  confirmAction('重新解析全部失败文档', '将重新解析失败文档并清除旧证据坐标，确定继续吗？', () => void onReparseAll())
}

async function onReparseAll(): Promise<void> {
  if (!task.value || reparseAllLoading.value) return
  reparseAllLoading.value = true
  overview.value = null
  evidenceMap.value = new Map()
  irEpoch.value++
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

function requestRetryCompare(): void {
  confirmAction('重新对比', '将重跑算法比对并重建全部比对证据，确定继续吗？', () => void onRetryCompare())
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

function requestRetryAi(): void {
  confirmAction('重新抽取', '将删除并重建 AI 证据（条款判定与关键指标），确定继续吗？', () => void onRetryAi())
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
    task.value = await triggerExtractClauses(task.value.id)
    startPoll()
  } catch {
    extracting.value = false
    message.error('条款提取失败，请稍后重试')
  }
}

async function onConfirmClauses(list: ClauseItem[]): Promise<void> {
  if (!task.value || confirmingClauses.value) return
  confirmingClauses.value = true
  try {
    task.value = await confirmClauses({
      taskId: task.value.id,
      clauses: list.map((c) => ({
        // 用户手动新增的条款没有后端 clauseId，提交时必须省略（本地 draft- id 不是服务端 id）
        ...(c.source === 'user_added' || c.id.startsWith('draft-') ? {} : { clauseId: c.id }),
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
const nameInputRef = ref<{ focus: () => void } | null>(null)

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
  drawerMounted.value = true
  drawerOpen.value = true
  if (workspaceRef.value) {
    applyPendingLocate()
    return
  }
  // 抽屉关闭时其内容并未渲染（antd 不渲染 slot），workspaceRef 为 null；
  // 打开后等抽屉内容挂载完成再应用定位，避免首次点击只开抽屉不跳页。
  void flushLocate()
}

async function flushLocate(): Promise<void> {
  for (let i = 0; i < 5 && pendingLocate.value; i++) {
    await nextTick()
    if (workspaceRef.value) {
      applyPendingLocate()
      return
    }
  }
}

function onLocateEvidence(ev: EvidenceItem): void {
  locateInDrawer({ kind: 'evidence', ev })
}

function onLocateRefs(refs: BlockRange[]): void {
  locateInDrawer({ kind: 'refs', refs })
}

/* —— 导出：setTimeout 链 + 次数上限，切任务/卸载时清理句柄与挂屏 toast —— */
const EXPORT_POLL_MS = 1500
const EXPORT_MAX_ATTEMPTS = 40
let exportTimer: number | null = null

function clearExportPoll(): void {
  if (exportTimer !== null) {
    window.clearTimeout(exportTimer)
    exportTimer = null
  }
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

function pollExport(taskId: string, exportId: string, attempt = 0): void {
  clearExportPoll()
  exportTimer = window.setTimeout(() => {
    exportTimer = null
    void (async () => {
      try {
        const st = await getExportStatus(taskId, exportId)
        if (disposed || task.value?.id !== taskId) return
        if (st.status === 'done') {
          exporting.value = false
          message.success({ content: '报告已生成', key: 'compare-export' })
          if (st.downloadUrl) downloadExport(st.downloadUrl)
        } else if (st.status === 'failed') {
          exporting.value = false
          exportError.value = '导出失败，可重试'
          message.error({ content: '导出失败，可重试', key: 'compare-export' })
        } else if (attempt + 1 >= EXPORT_MAX_ATTEMPTS) {
          exporting.value = false
          exportError.value = '导出超时，请稍后重试'
          message.warning({ content: '导出超时，请稍后重试', key: 'compare-export' })
        } else {
          pollExport(taskId, exportId, attempt + 1)
        }
      } catch {
        if (disposed || task.value?.id !== taskId) return
        exporting.value = false
        exportError.value = '导出状态查询失败，可重试'
        message.error({ content: '导出状态查询失败', key: 'compare-export' })
      }
    })()
  }, EXPORT_POLL_MS)
}

function downloadExport(url: string): void {
  const a = document.createElement('a')
  a.href = url
  a.download = ''
  a.rel = 'noopener'
  document.body.appendChild(a)
  a.click()
  a.remove()
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

.compare-read-select {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  gap: @spacing-md;
  max-width: 820px;
  margin: 0 auto @spacing-base;
  width: 100%;
  padding: @spacing-sm @spacing-base;
  background: @card-bg;
  border: 1px solid @border-color;
  border-radius: @radius-base;
}

.compare-read-select__label {
  flex-shrink: 0;
  font-size: @font-size-sm;
  font-weight: @font-weight-medium;
  color: @text-primary;
}

.compare-read-select__select {
  flex: 1;
  min-width: 0;
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
