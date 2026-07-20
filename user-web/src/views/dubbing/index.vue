<template>
  <div class="dubbing-page">
    <PageHeader title="AI配音" description="文本转语音，轻松生成配音">
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
        :speed="speed"
        :generating="producingTask"
        :producing-task="producingTask"
        :active-step="activeStep"
        :current-task="currentTask"
        @update:voice-id="(v: string) => selectedVoiceId = v"
        @update:text="(v: string) => text = v"
        @update:speed="(v: number) => speed = v"
        @generate="handleGenerate"
        @reset="currentTask = null"
      />

      <HistoryPanel
        v-else
        :tasks="historyTasks"
        :loading="historyLoading"
        @play="handlePlay"
        @delete="handleDelete"
        @re-edit="handleReEdit"
      />
    </div>

    <a-modal
      v-model:visible="playerVisible"
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
        >{{ textExpanded ? '收起配音文本' : '查看配音文本' }}</a-button>
        <p
          v-if="textExpanded"
          class="modal-player-text__content"
        >{{ currentTask?.text }}</p>
      </div>
      <DubbingPlayer v-if="currentTask && currentTask.status === '已完成'" :task="currentTask" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onBeforeUnmount } from 'vue'
import { message, notification } from 'ant-design-vue'
import PageHeader from '@shared/web/components/PageHeader.vue'
import OperationPanel from './components/OperationPanel.vue'
import HistoryPanel from './components/HistoryPanel.vue'
import DubbingPlayer from './components/DubbingPlayer.vue'
import {
  getVoices,
  generateDubbing,
  getDubbingTasks,
  getDubbingTask,
  deleteDubbingTask,
} from '@/api/modules/dubbing'
import type { VoiceItem, DubbingTask } from '@/types'

type TabKey = 'dub' | 'history'
const activeTab = ref<TabKey>('dub')
const tabOptions = [
  { label: '配音', value: 'dub' },
  { label: '历史记录', value: 'history' },
]

const voices = ref<VoiceItem[]>([])
const voicesLoading = ref(false)
const selectedVoiceId = ref('')
const speed = ref(1.0)
const text = ref('')
const historyTasks = ref<DubbingTask[]>([])
const historyLoading = ref(false)
const currentTask = ref<DubbingTask | null>(null)
const playerVisible = ref(false)
const textExpanded = ref(false)

const producingTask = ref(false)
const activeStep = ref(0)

const selectedVoiceName = computed(
  () => voices.value.find((v) => v.id === selectedVoiceId.value)?.name || '未选择',
)

let pollTimer: ReturnType<typeof setInterval> | null = null
function stopPolling(): void {
  if (pollTimer) {
    clearInterval(pollTimer)
    pollTimer = null
  }
}

async function fetchVoices(): Promise<void> {
  voicesLoading.value = true
  try {
    voices.value = await getVoices()
    if (voices.value.length > 0 && !selectedVoiceId.value) {
      selectedVoiceId.value = voices.value[0].id
    }
  } finally {
    voicesLoading.value = false
  }
}

async function fetchHistory(): Promise<void> {
  historyLoading.value = true
  try {
    const res = await getDubbingTasks({ skip: 0, max: 100 })
    historyTasks.value = res.items
  } finally {
    historyLoading.value = false
  }
}

async function handleGenerate(generateText: string): Promise<void> {
  if (producingTask.value) return
  producingTask.value = true
  activeStep.value = 0
  currentTask.value = null

  try {
    const task = await generateDubbing(generateText, selectedVoiceId.value, speed.value)
    currentTask.value = task
    activeStep.value = 1
    message.success('配音任务已提交，正在合成…')
    await fetchHistory()

    pollTimer = setInterval(async () => {
      try {
        const latest = await getDubbingTask(task.id)
        currentTask.value = latest
        if (latest.status === '已完成') {
          stopPolling()
          activeStep.value = 3
          producingTask.value = false
          await fetchHistory()
          notification.success({
            message: '配音生成完成',
            description: `「${latest.voiceName}」已完成合成，时长约 ${latest.durationSec ?? '-'} 秒。`,
            placement: 'topRight',
            duration: 4,
          })
        } else if (latest.status === '已失败') {
          stopPolling()
          activeStep.value = 3
          producingTask.value = false
          notification.error({
            message: '配音生成失败',
            description: '请检查文本内容后重试。',
            placement: 'topRight',
          })
        }
      } catch {
        stopPolling()
        producingTask.value = false
      }
    }, 800)
  } catch {
    stopPolling()
    producingTask.value = false
    message.error('生成失败，请重试')
  }
}

function handlePlay(task: DubbingTask): void {
  currentTask.value = task
  playerVisible.value = true
}

async function handleDelete(id: string): Promise<void> {
  try {
    await deleteDubbingTask(id)
    message.success('已删除')
    if (currentTask.value?.id === id) currentTask.value = null
    await fetchHistory()
  } catch {
    message.error('删除失败')
  }
}

function handleReEdit(task: DubbingTask): void {
  text.value = task.text
  if (task.voiceId) selectedVoiceId.value = task.voiceId
  currentTask.value = null
  activeTab.value = 'dub'
  message.success('已载入文本与音色，可在左侧调整')
}

onBeforeUnmount(stopPolling)

fetchVoices()
fetchHistory()
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

  :deep(.page-header) {
    margin-bottom: 0 !important;
  }
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
