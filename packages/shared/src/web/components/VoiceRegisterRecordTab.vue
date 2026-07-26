<template>
  <div>
    <div class="read-card">
      <p class="read-card__text">{{ recordText }}</p>
      <span class="read-card__dur">
        <svg width="10" height="10" viewBox="0 0 24 24" fill="none">
          <circle cx="12" cy="12" r="10" stroke="currentColor" stroke-width="2"/>
          <path d="M12 6V12L16 14" stroke="currentColor" stroke-width="2" stroke-linecap="round"/>
        </svg>
        ~15秒
      </span>
    </div>

    <div class="record-area">
      <div class="record-row">
        <button
          class="mic-btn"
          :class="{ 'mic-btn--rec': recording }"
          :title="recording ? '点击停止' : '点击录音'"
          @click="toggleRecord"
        >
          <span class="mic-btn__ring" />
          <svg v-if="!recording" width="18" height="18" viewBox="0 0 24 24" fill="none">
            <path d="M12 2C10.34 2 9 3.34 9 5V11C9 12.66 10.34 14 12 14C13.66 14 15 12.66 15 11V5C15 3.34 13.66 2 12 2Z" fill="currentColor"/>
            <path d="M20 11C20 15.08 16.42 18.24 12.5 18.88V22H11.5V18.88C7.58 18.24 4 15.08 4 11H5.5C5.5 14.31 8.69 17 12 17C15.31 17 18.5 14.31 18.5 11H20Z" fill="currentColor" fill-opacity="0.7"/>
          </svg>
          <svg v-else width="18" height="18" viewBox="0 0 24 24" fill="none">
            <rect x="6" y="4" width="4" height="16" rx="1" fill="currentColor"/>
            <rect x="14" y="4" width="4" height="16" rx="1" fill="currentColor"/>
          </svg>
        </button>

        <div class="wave" :class="{ 'wave--on': recording }">
          <div v-for="i in 28" :key="i" class="wave__bar" :style="waveStyle(i)" />
        </div>

        <div class="timer" :class="{ 'timer--on': recording }">
          {{ formattedTime }}<span class="timer__max">/ 00:15</span>
        </div>
      </div>
    </div>

    <transition name="slide-up">
      <div v-if="recordedBlob" class="file-card" @click="togglePlayRecorded">
        <svg v-if="!playing" class="file-card__play" width="14" height="14" viewBox="0 0 24 24" fill="none">
          <path d="M8 5V19L19 12L8 5Z" fill="currentColor"/>
        </svg>
        <svg v-else class="file-card__play" width="14" height="14" viewBox="0 0 24 24" fill="none">
          <rect x="6" y="4" width="4" height="16" rx="1" fill="currentColor"/>
          <rect x="14" y="4" width="4" height="16" rx="1" fill="currentColor"/>
        </svg>
        <span>录音完成 · 00:{{ String(Math.floor(recordElapsed)).padStart(2, '0') }}</span>
        <button class="file-card__del" @click.stop="clearRecorded">
          <svg width="12" height="12" viewBox="0 0 24 24" fill="none"><path d="M19 6.41L17.59 5L12 10.59L6.41 5L5 6.41L10.59 12L5 17.59L6.41 19L12 13.41L17.59 19L19 17.59L13.41 12L19 6.41Z" fill="currentColor"/></svg>
        </button>
      </div>
    </transition>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onUnmounted } from 'vue'
import { message } from 'ant-design-vue'

const emit = defineEmits<{
  audioReady: [blob: Blob]
  cleared: []
}>()

const recordText = '大家好，今天天气真不错。我来测试一下我的声音效果，希望录制顺利。这段录音会用来自动生成我的专属音色，以后就可以用自己的声音来配音了。'

let mediaRecorder: MediaRecorder | null = null
let audioChunks: Blob[] = []
let recordTimer: ReturnType<typeof setInterval> | null = null
const recording = ref(false)
const recordElapsed = ref(0)
const recordedBlob = ref<Blob | null>(null)

const playing = ref(false)
let playbackEl: HTMLAudioElement | null = null
let blobUrl: string | null = null

const formattedTime = computed(() => {
  const m = Math.floor(recordElapsed.value / 60)
  const s = recordElapsed.value % 60
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
})

function waveStyle(i: number) {
  if (!recording.value) {
    const h = 0.2 + (i % 5) * 0.12
    return { height: `${h * 100}%` }
  }
  return {}
}

async function toggleRecord(): Promise<void> {
  if (recording.value) { stopRecord(); return }
  if (recordedBlob.value) { clearRecorded() }
  try {
    const stream = await navigator.mediaDevices.getUserMedia({ audio: true })
    const mimeType = MediaRecorder.isTypeSupported('audio/webm;codecs=opus')
      ? 'audio/webm;codecs=opus'
      : 'audio/webm'
    mediaRecorder = new MediaRecorder(stream, { mimeType })
    audioChunks = []
    recordElapsed.value = 0

    mediaRecorder.ondataavailable = (e) => {
      if (e.data.size > 0) audioChunks.push(e.data)
    }
    mediaRecorder.onstop = () => {
      recordedBlob.value = new Blob(audioChunks, { type: mimeType })
      emit('audioReady', recordedBlob.value)
      stream.getTracks().forEach(t => t.stop())
    }

    mediaRecorder.start(100)
    recording.value = true
    recordTimer = setInterval(() => {
      recordElapsed.value++
      if (recordElapsed.value >= 15) stopRecord()
    }, 1000)
  } catch {
    message.error('麦克风访问被拒绝，请在浏览器设置中允许麦克风权限')
  }
}

function stopRecord(): void {
  if (recordTimer) { clearInterval(recordTimer); recordTimer = null }
  if (mediaRecorder && mediaRecorder.state !== 'inactive') mediaRecorder.stop()
  recording.value = false
  mediaRecorder = null
}

function clearRecorded(): void {
  recordedBlob.value = null
  audioChunks = []
  emit('cleared')
}

function togglePlayRecorded(): void {
  if (!recordedBlob.value) return
  if (playing.value) { stopPlayback(); return }
  blobUrl = URL.createObjectURL(recordedBlob.value)
  playbackEl = new Audio(blobUrl)
  playbackEl.addEventListener('ended', () => {
    stopPlayback()
    if (blobUrl) URL.revokeObjectURL(blobUrl)
  })
  playbackEl.play()
  playing.value = true
}

function stopPlayback(): void {
  if (playbackEl) { playbackEl.pause(); playbackEl = null }
  playing.value = false
}

onUnmounted(() => {
  stopPlayback()
  if (blobUrl) URL.revokeObjectURL(blobUrl)
  if (recordTimer) clearInterval(recordTimer)
  if (mediaRecorder) mediaRecorder.stream?.getTracks().forEach(t => t.stop())
})
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.read-card {
  background: color-mix(in srgb, @brand-primary 5%, transparent);
  border: 1px solid color-mix(in srgb, @brand-primary 10%, transparent);
  border-radius: @radius-base;
  padding: 10px 14px;
  margin-bottom: 14px;
  &__text {
    font-size: @font-size-sm;
    color: @text-primary;
    line-height: 1.7;
    margin: 0 0 6px;
  }
  &__dur {
    display: inline-flex; align-items: center; gap: 4px;
    font-size: 11px;
    color: @text-tertiary;
  }
}

.record-area { padding: 0; }

.record-row {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 14px;
}

.timer {
  flex-shrink: 0;
  white-space: nowrap;
  font-size: 22px;
  font-weight: @font-weight-semibold;
  font-variant-numeric: tabular-nums;
  color: @text-primary;
  letter-spacing: 0.5px;
  &--on { color: @danger; }
  &__max {
    font-size: @font-size-xs;
    color: @text-tertiary;
    margin-left: 3px;
    font-weight: @font-weight-regular;
  }
}

.wave {
  flex-shrink: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 2px; height: 36px;
  &__bar {
    width: 3px;
    min-height: 3px;
    border-radius: 2px;
    background: @border-color;
    transition: background 0.2s;
    flex-shrink: 0;
  }
  &--on &__bar {
    background: linear-gradient(to top, @brand-primary, @accent);
    animation: w 0.5s ease-in-out infinite alternate;
  }
}

@keyframes w {
  0% { transform: scaleY(0.3); }
  100% { transform: scaleY(1); }
}

.mic-btn {
  position: relative;
  flex-shrink: 0;
  width: 42px; height: 42px;
  padding: 0;
  border: none;
  background: linear-gradient(135deg, @brand-primary, @accent);
  border-radius: 50%;
  display: flex; align-items: center; justify-content: center;
  cursor: pointer;
  color: #fff;
  transition: all 0.25s cubic-bezier(0.34, 1.56, 0.64, 1);
  box-shadow: 0 3px 10px color-mix(in srgb, @brand-primary 25%, transparent);
  &__ring {
    position: absolute; inset: -3px; border-radius: 50%;
    border: 2px solid color-mix(in srgb, @brand-primary 15%, transparent);
    opacity: 0; transition: opacity 0.2s;
  }
  &:hover { transform: scale(1.05); box-shadow: 0 4px 14px color-mix(in srgb, @brand-primary 35%, transparent); }
  &:active { transform: scale(0.95); }
  &--rec {
    background: @danger;
    box-shadow: 0 3px 10px color-mix(in srgb, @danger 30%, transparent);
    animation: pulse 1.2s ease-in-out infinite;
    .mic-btn__ring {
      opacity: 1;
      animation: ring 1.2s ease-in-out infinite;
    }
  }
  svg { display: block; }
}

@keyframes pulse {
  0%, 100% { box-shadow: 0 3px 10px color-mix(in srgb, @danger 30%, transparent); }
  50% { box-shadow: 0 3px 20px color-mix(in srgb, @danger 45%, transparent); }
}
@keyframes ring {
  0% { transform: scale(1); opacity: 0.5; }
  100% { transform: scale(1.3); opacity: 0; }
}

.file-card {
  display: flex; align-items: center; gap: 8px;
  margin-top: 10px;
  padding: 8px 12px;
  background: color-mix(in srgb, @success 8%, transparent);
  border: 1px solid color-mix(in srgb, @success 18%, transparent);
  border-radius: @radius-base;
  font-size: @font-size-sm;
  color: @text-primary;
  cursor: pointer;
  transition: background 0.15s;
  &:hover { background: color-mix(in srgb, @success 12%, transparent); }
  &__play { flex-shrink: 0; color: @success; }
  span { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  &__del {
    flex-shrink: 0;
    width: 22px; height: 22px;
    border: none; background: transparent;
    border-radius: 4px;
    display: flex; align-items: center; justify-content: center;
    color: @text-tertiary; cursor: pointer;
    &:hover { background: color-mix(in srgb, @danger 10%, transparent); color: @danger; }
  }
}

@media (prefers-reduced-motion: reduce) {
  .wave__bar { animation: none !important; }
  .mic-btn--rec { animation: none; }
  .mic-btn__ring { display: none; }
}
</style>
