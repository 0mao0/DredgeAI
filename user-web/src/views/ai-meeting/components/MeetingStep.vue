<template>
  <SectionCard title="会议进行中" flush>
    <div class="meeting-step__recorder">
      <div class="meeting-step__equalizer" :class="{ 'is-active': isRecordingActive }">
        <span
          v-for="i in 5"
          :key="i"
          class="meeting-step__bar"
          :style="{ '--eq-delay': `${(i - 1) * 0.12}s` }"
        />
      </div>
      <div class="meeting-step__recorder-timer">{{ timerText }}</div>
      <div class="meeting-step__recorder-status" :class="{ 'is-recording': isRecordingActive, 'is-paused': paused }">
        <span class="meeting-step__recorder-dot" />
        <span>{{ statusText }}</span>
      </div>
      <div class="meeting-step__recorder-controls">
        <AppButton
          v-if="!recording"
          variant="primary"
          size="lg"
          :icon="h(PlayCircleFilled)"
          @click="onStart"
        >
          开始录音
        </AppButton>
        <template v-else>
          <AppButton
            shape="circle"
            size="lg"
            variant="primary"
            class="meeting-step__control"
            :icon="h(paused ? PlayCircleFilled : PauseCircleFilled)"
            :title="paused ? '继续录音' : '暂停录音'"
            @click="onTogglePause"
          />
          <AppButton
            shape="circle"
            size="lg"
            variant="danger"
            class="meeting-step__control"
            :icon="h(StopFilled)"
            title="停止录音"
            @click="onStop"
          />
        </template>
      </div>
    </div>

    <div class="meeting-step__transcript">
      <div v-if="transcript" class="meeting-step__transcript-text">{{ transcript }}</div>
      <div v-else class="meeting-step__transcript-empty">
        <AudioOutlined class="meeting-step__transcript-empty-icon" />
        <span>{{ asrError || '录音开始后，说话内容将自动显示在这里' }}</span>
      </div>
    </div>

    <p class="meeting-step__tip">
      <InfoCircleFilled class="meeting-step__tip-icon" />
      <span>会议结束后，系统会自动将录音转写并整理成报告（含出勤、转写稿、问答记录）。</span>
    </p>
    <AppButton variant="primary" size="lg" block :loading="loading" @click="onFinish">
      结束会议并生成报告
    </AppButton>
  </SectionCard>
</template>

<script setup lang="ts">
import { computed, h, onMounted, onScopeDispose, ref } from 'vue'
import { AudioOutlined, InfoCircleFilled, PauseCircleFilled, PlayCircleFilled, StopFilled } from '@ant-design/icons-vue'
import SectionCard from '@shared/web/components/SectionCard.vue'
import AppButton from '@shared/web/components/AppButton.vue'
import { transcribeAudio } from '@/api/modules/aiMeeting'
import { convertToWav16k } from '@/utils/audioToWav'
import { useRecorder } from '../composables/useRecorder'

defineProps<{
  loading: boolean
}>()
const emit = defineEmits<{
  finish: [recording: Blob]
}>()

const CHUNK_MS = 6000

const { recording, paused, start: startRecording, pause, resume, stop: stopRecording } = useRecorder()
const transcript = ref('')
const asrLoading = ref(false)
const asrError = ref('')
const elapsed = ref(0)
const segments: Blob[] = []
let timerHandle: number | undefined
let asrChain: Promise<void> = Promise.resolve()

const isRecordingActive = computed(() => recording.value && !paused.value)
const statusText = computed(() => {
  if (isRecordingActive.value) return '全程录音中'
  if (paused.value) return '录音已暂停'
  return '录音已停止'
})
const timerText = computed(() => {
  const m = Math.floor(elapsed.value / 60)
  const s = elapsed.value % 60
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
})

function onChunk(chunk: Blob): void {
  segments.push(chunk)
  asrChain = asrChain.then(async () => {
    if (paused.value) return
    asrLoading.value = true
    try {
      const wav = await convertToWav16k(chunk)
      const text = await transcribeAudio(wav)
      const trimmed = text.trim()
      if (trimmed) {
        transcript.value = [transcript.value, trimmed].filter(Boolean).join('\n')
        asrError.value = ''
      }
    } catch {
      // 单分片识别失败不打断录音，继续下一段
      asrError.value = '实时识别暂时不可用，录音仍在继续'
    } finally {
      asrLoading.value = false
    }
  })
}

onMounted(() => {
  void startRecording({ timeslice: CHUNK_MS, onChunk })
  timerHandle = window.setInterval(() => {
    if (isRecordingActive.value) elapsed.value += 1
  }, 1000)
})

onScopeDispose(() => {
  if (timerHandle !== undefined) window.clearInterval(timerHandle)
  void stopRecording()
})

async function onStart(): Promise<void> {
  asrError.value = ''
  try {
    await startRecording({ timeslice: CHUNK_MS, onChunk })
  } catch {
    asrError.value = '无法访问麦克风，请检查浏览器权限'
  }
}

function onTogglePause(): void {
  if (paused.value) {
    resume()
  } else {
    pause()
  }
}

async function onStop(): Promise<void> {
  await stopRecording()
}

async function onFinish(): Promise<void> {
  await stopRecording()
  const audio = segments.length > 0
    ? new Blob(segments, { type: 'audio/webm' })
    : new Blob()
  emit('finish', audio)
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.meeting-step__recorder {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-xl @spacing-base @spacing-lg;
  margin-bottom: @spacing-lg;
  border-radius: @radius-lg;
  border: 1px solid @border-color;
  background: linear-gradient(
    165deg,
    color-mix(in srgb, var(--color-brand) 8%, var(--color-card-bg)) 0%,
    var(--color-card-bg) 55%
  );
}
.meeting-step__equalizer {
  display: flex;
  align-items: flex-end;
  gap: 4px;
  height: 22px;
}
.meeting-step__bar {
  width: 4px;
  height: 100%;
  border-radius: 2px;
  background: color-mix(in srgb, var(--color-brand) 60%, transparent);
  transform: scaleY(0.3);
  transform-origin: bottom;
  transition: transform @transition-slow;

  .is-active & {
    animation: meeting-step-eq 0.9s ease-in-out infinite alternate;
    animation-delay: var(--eq-delay);
  }
}
.meeting-step__recorder-timer {
  font-size: @font-size-4xl;
  font-weight: @font-weight-semibold;
  font-variant-numeric: tabular-nums;
  line-height: 1.1;
  color: @text-primary;
  letter-spacing: 2px;
}
.meeting-step__recorder-status {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
  font-size: @font-size-sm;
  color: @text-secondary;

  &.is-recording {
    color: @danger;
  }
  &.is-paused {
    color: @warning;
  }
}
.meeting-step__recorder-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: @text-tertiary;

  .is-recording & {
    background: @danger;
    animation: meeting-step-pulse 1.2s ease-in-out infinite;
  }
  .is-paused & {
    background: @warning;
  }
}
.meeting-step__recorder-controls {
  display: flex;
  gap: @spacing-base;
  margin-top: @spacing-sm;
}
.meeting-step__control {
  transition: transform @transition-fast;

  &:hover {
    transform: scale(1.06);
  }
  &:active {
    transform: scale(0.96);
  }
}

.meeting-step__transcript {
  margin-bottom: @spacing-lg;
  border: 1px solid @border-color;
  border-radius: @radius-base;
  background: @content-bg;
  overflow: hidden;
}
.meeting-step__transcript-text {
  max-height: 240px;
  overflow-y: auto;
  padding: @spacing-base;
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.8;
  font-size: @font-size-sm;
  color: @text-primary;
}
.meeting-step__transcript-empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: @spacing-sm;
  padding: @spacing-2xl @spacing-base;
  text-align: center;
  font-size: @font-size-sm;
  color: @text-tertiary;
}
.meeting-step__transcript-empty-icon {
  font-size: 22px;
  color: @text-tertiary;
}

.meeting-step__tip {
  display: flex;
  align-items: flex-start;
  gap: @spacing-xs;
  margin: 0 0 @spacing-lg;
  font-size: @font-size-sm;
  color: @text-tertiary;
  line-height: 1.6;
}
.meeting-step__tip-icon {
  margin-top: 3px;
  flex: none;
  color: @brand-primary;
}

@keyframes meeting-step-eq {
  from {
    transform: scaleY(0.3);
  }
  to {
    transform: scaleY(1);
  }
}
@keyframes meeting-step-pulse {
  0%, 100% {
    opacity: 1;
    transform: scale(1);
  }
  50% {
    opacity: 0.45;
    transform: scale(0.8);
  }
}

@media (prefers-reduced-motion: reduce) {
  .meeting-step__equalizer .meeting-step__bar,
  .meeting-step__recorder-dot {
    animation: none !important;
  }
  .meeting-step__equalizer.is-active .meeting-step__bar {
    transform: scaleY(0.85);
  }
}
</style>
