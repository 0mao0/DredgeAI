<template>
  <div class="audio-player">
    <audio
      v-if="!controlled"
      ref="audioRef"
      :src="src"
      preload="auto"
      @loadedmetadata="onLoaded"
      @timeupdate="onTimeUpdate"
      @ended="onEnded"
    />

    <div class="audio-player__progress">
      <a-slider
        :value="controlled ? controlled.progress * 100 : currentTime"
        :max="controlled ? 100 : duration || 1"
        :disabled="Boolean(controlled)"
        :tooltip-visible="false"
        @update:value="controlled ? undefined : seek"
      />
    </div>

    <div class="audio-player__controls">
      <AppButton
        shape="circle"
        :icon="playIcon"
        size="lg"
        :loading="controlled?.loading"
        class="audio-player__play"
        @click="togglePlay"
      />
      <div class="audio-player__time">
        <span>{{ formatTime(controlled ? controlled.currentTime : currentTime) }}</span>
        <span class="audio-player__separator">/</span>
        <span>{{ formatTime(controlled ? controlled.duration : duration) }}</span>
      </div>
      <div class="audio-player__speed-group">
        <span class="audio-player__speed-label">倍速</span>
        <a-radio-group :value="playbackRate" size="small" @change="onSpeedChange">
          <a-radio-button :value="0.5">0.5x</a-radio-button>
          <a-radio-button :value="1">1x</a-radio-button>
          <a-radio-button :value="1.5">1.5x</a-radio-button>
          <a-radio-button :value="2">2x</a-radio-button>
        </a-radio-group>
      </div>
      <slot name="extra" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, h, onBeforeUnmount, ref } from 'vue'
import { PlayCircleFilled, PauseCircleFilled } from '@ant-design/icons-vue'
import AppButton from './AppButton.vue'

const props = defineProps<{
  /** 音频 URL（配音任务产物、TTS 整段音频等） */
  src?: string
  /**
   * 受控播放模式：由外部提供播放状态（分段流式 TTS 等无单一 URL 的场景）。
   * 传入后组件只渲染播放 UI，不管理内部 <audio>。
   */
  controlled?: {
    playing: boolean
    loading?: boolean
    progress: number
    currentTime: number
    duration: number
    onToggle: () => void
  }
}>()

const audioRef = ref<HTMLAudioElement | null>(null)
const isPlaying = ref(false)
const currentTime = ref(0)
const duration = ref(0)
const playbackRate = ref(1)

const playIcon = computed(() =>
  h(
    (props.controlled ? props.controlled.playing : isPlaying.value)
      ? PauseCircleFilled
      : PlayCircleFilled,
  ),
)

function formatTime(sec: number): string {
  if (!sec || Number.isNaN(sec)) return '00:00'
  const m = Math.floor(sec / 60)
  const s = Math.floor(sec % 60)
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
}

function onLoaded(): void {
  if (audioRef.value) {
    duration.value = audioRef.value.duration
    audioRef.value.playbackRate = playbackRate.value
  }
}

function onTimeUpdate(): void {
  if (audioRef.value) currentTime.value = audioRef.value.currentTime
}

function onEnded(): void {
  isPlaying.value = false
}

function onSpeedChange(e: Event): void {
  const target = e.target as HTMLInputElement
  const rate = Number(target.value)
  playbackRate.value = rate
  if (audioRef.value) audioRef.value.playbackRate = rate
}

function seek(val: number): void {
  if (audioRef.value) {
    audioRef.value.currentTime = val
    currentTime.value = val
  }
}

function togglePlay(): void {
  if (props.controlled) {
    props.controlled.onToggle()
    return
  }
  if (!audioRef.value) return
  if (isPlaying.value) {
    audioRef.value.pause()
    isPlaying.value = false
  } else {
    void audioRef.value.play()
    isPlaying.value = true
  }
}

onBeforeUnmount(() => {
  audioRef.value?.pause()
  audioRef.value = null
})
</script>

<style scoped lang="less">
@import '../styles/variables.less';

.audio-player__progress {
  margin-bottom: @spacing-md;

  :deep(.ant-slider) {
    margin: 0;
  }
}
.audio-player__controls {
  display: flex;
  align-items: center;
  gap: @spacing-md;
  flex-wrap: wrap;
}
.audio-player__play {
  transition: transform 0.15s ease, box-shadow 0.2s ease;

  &:hover {
    transform: scale(1.08);
    box-shadow: @shadow-sm;
  }
  &:active {
    transform: scale(0.96);
  }
}
.audio-player__time {
  font-size: @font-size-sm;
  color: @text-primary;
  font-variant-numeric: tabular-nums;
  white-space: nowrap;
}
.audio-player__separator {
  margin: 0 @spacing-xs;
  color: @text-tertiary;
}
.audio-player__speed-group {
  display: flex;
  align-items: center;
  gap: @spacing-xs;
  margin-left: auto;
}
.audio-player__speed-label {
  font-size: @font-size-xs;
  color: @text-tertiary;
  white-space: nowrap;
}
</style>
