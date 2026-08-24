<template>
  <div class="speech-player">
    <AppButton
      shape="circle"
      size="lg"
      class="speech-player__btn"
      :loading="synthesizing"
      @click="onToggle"
    >
      <PauseCircleFilled v-if="playing && !synthesizing" />
      <PlayCircleFilled v-else />
    </AppButton>
    <a-slider
      class="speech-player__bar"
      :value="progress"
      :max="1"
      :step="0.01"
      :tooltip-visible="false"
      :disabled="!playing"
    />
    <div class="speech-player__time">{{ formatTime(currentTime) }} / {{ formatTime(duration) }}</div>
  </div>
</template>

<script setup lang="ts">
import { onScopeDispose, watch } from 'vue'
import { PlayCircleFilled, PauseCircleFilled } from '@ant-design/icons-vue'
import AppButton from '@shared/web/components/AppButton.vue'
import { useSpeechPlayback } from '../composables/useSpeechPlayback'

const props = defineProps<{
  text: string
  autoPlay?: boolean
}>()

const { playing, synthesizing, progress, currentTime, duration, play, stop } = useSpeechPlayback()

watch(
  () => props.text,
  (text) => {
    if (text && props.autoPlay) {
      void play(text)
    }
  },
  { immediate: true },
)

function onToggle(): void {
  if (playing.value) {
    stop()
  } else if (props.text) {
    void play(props.text)
  }
}

onScopeDispose(() => stop())

function formatTime(sec: number): string {
  if (!sec || Number.isNaN(sec)) return '00:00'
  const m = Math.floor(sec / 60)
  const s = Math.floor(sec % 60)
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
}
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.speech-player {
  display: flex;
  align-items: center;
  gap: @spacing-md;
  padding: @spacing-sm @spacing-md;
  background: @content-bg;
  border-radius: @radius-lg;
}
.speech-player__btn {
  flex-shrink: 0;
  font-size: 26px;
  color: @brand-primary;
}
.speech-player__bar {
  flex: 1;
  min-width: 0;
}
.speech-player__time {
  flex-shrink: 0;
  font-size: @font-size-xs;
  color: @text-tertiary;
  font-variant-numeric: tabular-nums;
}
</style>
