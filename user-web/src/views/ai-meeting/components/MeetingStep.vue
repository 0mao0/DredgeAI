<template>
  <SectionCard title="会议进行中" flush>
    <a-alert
      type="info"
      show-icon
      :message="meetingRecording ? '全程录音中…按住下方按钮提问，松开后自动识别并回答' : '录音未开始'"
    />
    <AppButton
      variant="primary"
      size="lg"
      block
      :loading="loading"
      @pointerdown="onPttPress"
      @pointerup="onPttRelease"
    >
      {{ pttRecording ? '松开发问' : '按住说话' }}
    </AppButton>
    <a-input-search
      v-model:value="question"
      placeholder="也可以输入文字提问"
      enter-button="提问"
      @search="onAskText"
    />
    <div class="meeting-step__qa">
      <div v-for="qa in qaRecords" :key="qa.id" class="meeting-step__qa-item">
        <div><b>问：</b>{{ qa.question }}</div>
        <div class="meeting-step__qa-answer">
          <b>答：</b>{{ qa.answer }}
          <a-button size="small" type="link" :loading="playingId === qa.id" @click="playById(qa.id)">
            🔊 播放
          </a-button>
        </div>
      </div>
    </div>
    <AppButton variant="primary" size="lg" block :loading="loading" @click="onFinish">
      结束会议并生成报告
    </AppButton>
  </SectionCard>
</template>

<script setup lang="ts">
import { onMounted, ref, toRef } from 'vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import type { QaRecordDto } from '@/types'
import { useRecorder } from '../composables/useRecorder'
import { useAudioPlayer } from '../composables/useAudioPlayer'
import { useQaAudio } from '../composables/useQaAudio'

const props = defineProps<{ loading: boolean, qaRecords: QaRecordDto[] }>()
const emit = defineEmits<{
  askText: [question: string]
  askAudio: [audio: Blob]
  finish: [recording: Blob]
}>()

const { play } = useAudioPlayer()
const { pendingVoice, playingId, playById } = useQaAudio(toRef(props, 'qaRecords'), play)
const { recording: pttRecording, start: startPtt, stop: stopPtt } = useRecorder()
const { recording: meetingRecording, start: startMeetingRec, stop: stopMeetingRec } = useRecorder()
const question = ref('')

onMounted(() => {
  void startMeetingRec()
})

async function onPttPress(): Promise<void> {
  await startPtt()
}

async function onPttRelease(): Promise<void> {
  const audio = await stopPtt()
  if (audio.size === 0) return
  pendingVoice.value = true
  emit('askAudio', audio)
}

function onAskText(): void {
  if (!question.value.trim()) return
  emit('askText', question.value.trim())
  question.value = ''
}

async function onFinish(): Promise<void> {
  const audio = await stopMeetingRec()
  emit('finish', audio)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.meeting-step__qa {
  margin-top: @spacing-md;
}
.meeting-step__qa-item {
  padding: @spacing-sm @spacing-md;
  background: @content-bg;
  border-radius: @radius-base;
  margin-bottom: @spacing-sm;
}
.meeting-step__qa-answer {
  display: flex;
  align-items: baseline;
  gap: @spacing-xs;
}
</style>
