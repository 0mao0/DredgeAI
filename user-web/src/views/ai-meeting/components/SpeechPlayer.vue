<template>
  <AudioPlayer :controlled="controlled" class="speech-player" />
</template>

<script setup lang="ts">
import { computed, onScopeDispose, watch } from 'vue'
import { AudioPlayer } from '@shared/web'
import { useSpeechPlayback } from '../composables/useSpeechPlayback'

const props = defineProps<{
  text: string
  autoPlay?: boolean
}>()

const { playing, synthesizing, progress, currentTime, duration, play, stop } = useSpeechPlayback()

const controlled = computed(() => ({
  playing: playing.value,
  loading: synthesizing.value,
  progress: progress.value,
  currentTime: currentTime.value,
  duration: duration.value,
  onToggle: () => {
    if (playing.value) {
      stop()
    } else if (props.text) {
      void play(props.text)
    }
  },
}))

watch(
  () => props.text,
  (text) => {
    if (text && props.autoPlay) {
      void play(text)
    }
  },
  { immediate: true },
)

onScopeDispose(() => stop())
</script>

<style scoped lang="less">
.speech-player {
  width: 100%;
}
</style>
