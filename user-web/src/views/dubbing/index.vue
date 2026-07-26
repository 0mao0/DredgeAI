<template>
  <div class="dubbing-page">
    <PageHeader title="AI配音">
      <template #extra>
        <a-segmented
          v-model:value="activeTab"
          :options="tabOptions"
          class="dubbing-tabs"
        />
      </template>
    </PageHeader>

    <div class="dubbing-body">
      <OperationPanel
        v-if="activeTab === 'dub'"
        :voices="voices"
        :voices-loading="voicesLoading"
        :voice-id="selectedVoiceId"
        :selected-voice-name="selectedVoiceName"
        :text="text"
        :generating="producingTask"
        :producing-task="producingTask"
        :active-step="activeStep"
        :current-task="currentTask"
        @update:voice-id="(v: string) => selectedVoiceId = v"
        @update:text="(v: string) => text = v"
        @generate="handleGenerate"
        @reset="currentTask = null"
        @open-register="registerModalVisible = true"
        @delete-voice="handleVoiceDeleted"
        @show-fail-detail="handleShowFailDetail"
      />

      <HistoryPanel
        v-else
        :tasks="historyTasks"
        :loading="false"
        @play="handlePlay"
        @delete="handleDelete"
        @re-edit="handleReEdit"
      />
    </div>

    <VoiceRegisterModal
      v-model:open="registerModalVisible"
      :initial-tab="registerInitialTab"
      @confirmed="handleVoiceConfirmed"
    />

    <!-- 上传失败详情弹框 -->
    <a-modal
      v-model:open="failDetailVisible"
      :title="failedVoice?.name || '转换失败'"
      width="440px"
      destroy-on-close
    >
      <div class="fail-detail">
        <div class="fail-detail__icon">
          <CloseCircleFilled />
        </div>
        <div class="fail-detail__body">
          <p class="fail-detail__title">音色转换失败</p>
          <p class="fail-detail__reason">{{ failedVoice?.failReason || '未知错误，请稍后重试' }}</p>
        </div>
      </div>
      <template #footer>
        <a-button @click="handleDeleteFailedVoice">删除音色</a-button>
        <a-button @click="failDetailVisible = false">取消</a-button>
        <a-button @click="handleRetry('upload')">重新上传</a-button>
        <a-button type="primary" @click="handleRetry('record')">重新录制</a-button>
      </template>
    </a-modal>

    <a-modal
      v-model:open="playerVisible"
      :title="currentTask?.voiceName || '播放配音'"
      :footer="null"
      width="520px"
      destroy-on-close
      class="player-modal"
    >
      <div class="modal-player-text">
        <a-button
          type="link"
          size="small"
          class="modal-player-text__toggle"
          @click="textExpanded = !textExpanded"
        >
          {{ textExpanded ? '收起配音文本' : '查看配音文本' }}
        </a-button>
        <p
          v-if="textExpanded"
          class="modal-player-text__content"
        >
          {{ currentTask?.text }}
        </p>
      </div>
      <DubbingPlayer v-if="currentTask && currentTask.status === '已完成'" :task="currentTask" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import { message, notification } from 'ant-design-vue'
import { CloseCircleFilled } from '@ant-design/icons-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import OperationPanel from './components/OperationPanel.vue'
import HistoryPanel from './components/HistoryPanel.vue'
import DubbingPlayer from './components/DubbingPlayer.vue'
import VoiceRegisterModal from '@shared/web/components/VoiceRegisterModal.vue'
import { getVoices, generateDubbing, registerVoice } from '@/api/modules/dubbing'
import type { VoiceItem, DubbingTask } from '@/types'

const HISTORY_KEY = 'DREDGE_AI_DUBBING_HISTORY'
const PRIVATE_VOICES_KEY = 'DREDGE_AI_PRIVATE_VOICES'

type TabKey = 'dub' | 'history'
const activeTab = ref<TabKey>('dub')
const tabOptions = [
  { label: '配音', value: 'dub' },
  { label: '历史记录', value: 'history' },
]

const voices = ref<VoiceItem[]>([])
const voicesLoading = ref(false)
const selectedVoiceId = ref('')
const text = ref('')
const historyTasks = ref<DubbingTask[]>([])
const currentTask = ref<DubbingTask | null>(null)
const playerVisible = ref(false)
const textExpanded = ref(false)

const producingTask = ref(false)
const activeStep = ref(0)
const registerModalVisible = ref(false)
const registerInitialTab = ref<'record' | 'upload'>('record')
const failDetailVisible = ref(false)
const failedVoice = ref<VoiceItem | null>(null)

const selectedVoiceName = computed(
  () => voices.value.find((v) => v.id === selectedVoiceId.value)?.name || '未选择',
)

function loadPrivateVoices(): VoiceItem[] {
  try {
    return JSON.parse(localStorage.getItem(PRIVATE_VOICES_KEY) || '[]')
  } catch {
    return []
  }
}

function savePrivateVoice(voice: VoiceItem): void {
  const list = loadPrivateVoices().filter((v) => v.id !== voice.id)
  list.unshift(voice)
  localStorage.setItem(PRIVATE_VOICES_KEY, JSON.stringify(list))
}

function loadHistory(): void {
  try {
    const raw = localStorage.getItem(HISTORY_KEY)
    historyTasks.value = raw ? (JSON.parse(raw) as DubbingTask[]) : []
  } catch {
    historyTasks.value = []
  }
}

function saveHistory(): void {
  try {
    localStorage.setItem(HISTORY_KEY, JSON.stringify(historyTasks.value.slice(0, 100)))
  } catch {
    /* 忽略存储异常 */
  }
}

/** 从音频二进制估算时长（秒），带超时兜底，避免阻塞结果展示 */
function readDuration(blob: Blob): Promise<number> {
  return new Promise((resolve) => {
    const url = URL.createObjectURL(blob)
    const audio = document.createElement('audio')
    audio.preload = 'metadata'
    const done = (sec: number) => {
      resolve(sec)
      URL.revokeObjectURL(url)
    }
    const timer = setTimeout(done, 5000, 0)
    audio.onloadedmetadata = () => {
      clearTimeout(timer)
      done(Number.isFinite(audio.duration) ? Math.round(audio.duration * 10) / 10 : 0)
    }
    audio.onerror = () => {
      clearTimeout(timer)
      done(0)
    }
    audio.src = url
  })
}

async function fetchVoices(): Promise<void> {
  voicesLoading.value = true
  const withUrl = (v: VoiceItem) => ({ ...v, sampleUrl: `/tts/samples/${v.id}.wav` })
  let loaded: VoiceItem[] = []

  // Load from server
  try {
    loaded = (await getVoices()).map((v) => withUrl({ ...v, visibility: v.visibility || 'public' }))
  } catch {
    message.error('音色列表加载失败，请确认 TTS 服务已启动')
  }

  // Always load private voices from localStorage (exclude soft-deleted ones)
  const stored = loadPrivateVoices().filter((v) => !v.deletedByUser).map((v) => withUrl({ ...v, visibility: v.visibility || 'private' }))
  for (const pv of stored) {
    const idx = loaded.findIndex((v) => v.id === pv.id)
    if (idx >= 0) {
      loaded[idx] = pv
    } else {
      loaded.push(pv)
    }
  }
  voices.value = loaded
  if (voices.value.length > 0 && !selectedVoiceId.value) {
    selectedVoiceId.value = voices.value[0].id
  }
  voicesLoading.value = false
}

function handleVoiceConfirmed(payload: { voice: VoiceItem, formData: FormData }): void {
  const { voice, formData } = payload
  // Add to list immediately with 'converting' status
  voices.value = [voice, ...voices.value.filter((v) => v.id !== voice.id)]
  selectedVoiceId.value = voice.id

  // Upload in background — modal already closed
  registerVoice(formData)
    .then((result) => {
      const updated: VoiceItem = {
        ...voice,
        id: result.voice_id,
        sampleUrl: `/tts/samples/${result.voice_id}.wav`,
        uploadStatus: 'ready',
      }
      // Replace temp voice with real one, also clean up any converting/failed duplicates with same name
      voices.value = [
        updated,
        ...voices.value.filter((v) =>
          v.id !== voice.id
          && !(v.name === voice.name && v.uploadStatus && v.uploadStatus !== 'ready'),
        ),
      ]
      savePrivateVoice(updated)
      // Also purge duplicates and soft-deleted from localStorage
      const stored = loadPrivateVoices().filter((v) =>
        !v.deletedByUser && !(v.name === voice.name && v.uploadStatus && v.uploadStatus !== 'ready'),
      )
      localStorage.setItem(PRIVATE_VOICES_KEY, JSON.stringify(stored))
      if (selectedVoiceId.value === voice.id) {
        selectedVoiceId.value = result.voice_id
      }
      message.success(`音色「${updated.name}」已创建`)
    })
    .catch((err) => {
      // If a ready voice with same name already exists, silently discard this duplicate
      const alreadyReady = voices.value.some(
        (v) => v.name === voice.name && v.id !== voice.id && v.uploadStatus === 'ready',
      )
      if (alreadyReady) {
        voices.value = voices.value.filter((v) => v.id !== voice.id)
        return
      }
      const reason = err instanceof Error ? err.message : '服务器处理失败，请检查 TTS 服务是否正常运行'
      const failed: VoiceItem = {
        ...voice,
        uploadStatus: 'failed',
        failReason: reason,
      }
      voices.value = [
        failed,
        ...voices.value.filter((v) => v.id !== voice.id),
      ]
      savePrivateVoice(failed)
      message.error(`音色「${voice.name}」转换失败`)
    })
}

function handleVoiceDeleted(voiceId: string): void {
  // Soft delete: keep in localStorage with deletedByUser flag for admin visibility
  const voice = voices.value.find((v) => v.id === voiceId)
  if (voice) {
    const deleted = { ...voice, deletedByUser: true }
    const stored = loadPrivateVoices().filter((v) => v.id !== voiceId)
    stored.unshift(deleted)
    localStorage.setItem(PRIVATE_VOICES_KEY, JSON.stringify(stored))
  }
  voices.value = voices.value.filter((v) => v.id !== voiceId)
  if (selectedVoiceId.value === voiceId) {
    selectedVoiceId.value = voices.value[0]?.id || ''
  }
}

function handleShowFailDetail(voice: VoiceItem): void {
  failedVoice.value = voice
  failDetailVisible.value = true
}

function handleDeleteFailedVoice(): void {
  if (failedVoice.value) {
    handleVoiceDeleted(failedVoice.value.id)
  }
  failDetailVisible.value = false
  failedVoice.value = null
}

function handleRetry(tab: 'record' | 'upload'): void {
  failDetailVisible.value = false
  // Remove the failed voice from list
  if (failedVoice.value) {
    handleVoiceDeleted(failedVoice.value.id)
  }
  failedVoice.value = null
  registerInitialTab.value = tab
  registerModalVisible.value = true
}

async function handleGenerate(generateText: string): Promise<void> {
  if (producingTask.value) return
  if (!selectedVoiceId.value) {
    message.warning('请先选择音色')
    return
  }
  producingTask.value = true
  activeStep.value = 0
  currentTask.value = null

  try {
    const blob = await generateDubbing(generateText, selectedVoiceId.value, 1.0)
    if (!blob || blob.size === 0) throw new Error('empty audio')

    const audioUrl = URL.createObjectURL(blob)
    const voice = voices.value.find((v) => v.id === selectedVoiceId.value)
    const charCount = generateText.length
    const task: DubbingTask = {
      id: `local-${Date.now()}`,
      text: generateText,
      charCount,
      voiceId: selectedVoiceId.value,
      voiceName: voice?.name || selectedVoiceId.value,
      category: voice?.category || '',
      speed: 1.0,
      status: '已完成',
      audioUrl,
      durationSec: 0,
      tokenCost: Math.ceil(charCount / 1.5) + 50,
      createdAt: new Date().toISOString(),
    }
    currentTask.value = task
    activeStep.value = 3
    producingTask.value = false

    readDuration(blob).then((sec) => {
      if (currentTask.value && currentTask.value.id === task.id) {
        currentTask.value = { ...currentTask.value, durationSec: sec }
      }
    })

    historyTasks.value = [task, ...historyTasks.value]
    saveHistory()

    notification.success({
      message: '配音生成完成',
      description: `「${task.voiceName}」已完成合成。`,
      placement: 'topRight',
      duration: 4,
    })
  } catch (e) {
    producingTask.value = false
    activeStep.value = 3
    const msg = e instanceof Error ? e.message : ''
    message.error(`生成失败，请检查 TTS 服务或文本后重试${msg ? `（${msg}）` : ''}`)
  }
}

function handlePlay(task: DubbingTask): void {
  currentTask.value = task
  playerVisible.value = true
}

function handleDelete(id: string): void {
  historyTasks.value = historyTasks.value.filter((t) => t.id !== id)
  saveHistory()
  if (currentTask.value?.id === id) currentTask.value = null
  message.success('已删除')
}

function handleReEdit(task: DubbingTask): void {
  text.value = task.text
  if (task.voiceId) selectedVoiceId.value = task.voiceId
  currentTask.value = null
  activeTab.value = 'dub'
  message.success('已载入文本与音色，可在左侧调整')
}

loadHistory()
fetchVoices()
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.dubbing-page {
  height: 100%;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  padding: @page-padding;
  box-sizing: border-box;

  :deep(.page-desc) {
    margin-top: 0 !important;
  }
}

.dubbing-tabs {
  margin-right: @spacing-md;
}

.dubbing-body {
  flex: 1;
  min-height: 0;
}

.modal-player-text {
  margin-bottom: @spacing-lg;
  &__content {
    font-size: @font-size-sm;
    color: @text-secondary;
    line-height: 1.6;
    margin: @spacing-sm 0 0;
    padding: @spacing-md;
    background: @content-bg;
    border: 1px solid @border-color;
    border-radius: @radius-base;
    max-height: 140px;
    overflow: auto;
    white-space: pre-wrap;
    word-break: break-word;
  }
  &__toggle {
    padding: 0;
    height: auto;
  }
}

@media (max-width: 991px) {
  .dubbing-page {
    height: auto;
    overflow: visible;
  }
}
</style>
