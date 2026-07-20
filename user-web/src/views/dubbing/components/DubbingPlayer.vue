<template>
  <div class="player">
    <audio
      ref="audioRef"
      :src="task.audioUrl"
      preload="auto"
      @loadedmetadata="onLoaded"
      @timeupdate="onTimeUpdate"
      @ended="onEnded"
    />

    <div class="player__progress">
      <a-slider :value="currentTime" :max="duration || 1" :tooltip-visible="false" @update:value="seek" />
    </div>

    <div class="player__controls">
      <a-button
        shape="circle"
        :icon="playIcon"
        size="large"
        class="player__play"
        @click="togglePlay"
      />

      <div class="player__time">
        <span>{{ formatTime(currentTime) }}</span>
        <span class="player__separator">/</span>
        <span>{{ formatTime(duration) }}</span>
      </div>

      <div class="player__speed-group">
        <span class="player__speed-label">倍速</span>
        <a-radio-group :value="playbackRate" size="small" @change="onSpeedChange">
          <a-radio-button :value="0.5">0.5x</a-radio-button>
          <a-radio-button :value="1">1x</a-radio-button>
          <a-radio-button :value="1.5">1.5x</a-radio-button>
          <a-radio-button :value="2">2x</a-radio-button>
        </a-radio-group>
      </div>

      <a-tooltip title="下载音频">
        <a-button
          v-if="task.status === '已完成' && task.audioUrl"
          :href="task.audioUrl"
          download="dubbing-audio.mp3"
          class="player__download"
        >
          <DownloadOutlined /> 下载
        </a-button>
      </a-tooltip>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onBeforeUnmount } from 'vue'
import { h } from 'vue'
import { PlayCircleFilled, PauseCircleFilled, DownloadOutlined } from '@ant-design/icons-vue'
import type { DubbingTask } from '@/types'

defineProps<{
  task: DubbingTask
}>()

const audioRef = ref<HTMLAudioElement | null>(null)
const isPlaying = ref(false)
const currentTime = ref(0)
const duration = ref(0)
const playbackRate = ref(1)

/** 动态生成播放/暂停图标VNode */
const playIcon = computed(() =>
  h(isPlaying.value ? PauseCircleFilled : PlayCircleFilled),
)

function formatTime(sec: number): string {
  if (!sec || isNaN(sec)) return '00:00'
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

function onSpeedChange(e: any): void {
  const rate = Number(e.target.value)
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
  if (!audioRef.value) return
  if (isPlaying.value) {
    audioRef.value.pause()
    isPlaying.value = false
  } else {
    audioRef.value.play()
    isPlaying.value = true
  }
}

onBeforeUnmount(() => {
  audioRef.value?.pause()
  audioRef.value = null
})
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.player {
  &__progress {
    margin-bottom: @spacing-md;
    :deep(.ant-slider) { margin: 0; }
  }
  &__controls {
    display: flex;
    align-items: center;
    gap: @spacing-md;
    flex-wrap: wrap;
  }
  &__play {
    transition: transform 0.15s ease, box-shadow 0.2s ease;
    &:hover { transform: scale(1.08); box-shadow: @shadow-sm; }
    &:active { transform: scale(0.96); }
  }
  &__time {
    font-size: @font-size-sm;
    color: @text-primary;
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }
  &__separator { margin: 0 @spacing-xs; color: @text-tertiary; }
  &__speed-group {
    display: flex;
    align-items: center;
    gap: @spacing-xs;
    margin-left: auto;
  }
  &__speed-label { font-size: @font-size-xs; color: @text-tertiary; white-space: nowrap; }
  &__download { margin-left: 0; }
}

@media (prefers-reduced-motion: reduce) {
  .player__play { transition: box-shadow 0.2s ease; &:hover, &:active { transform: none; } }
}
</style>
