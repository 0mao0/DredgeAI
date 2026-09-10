<template>
  <div class="speech-player">
    <AudioPlayer :controlled="controlled" class="speech-player__player">
      <template #extra>
        <span v-if="synthesizing" class="speech-player__hint">{{ synthesizingHint }}</span>
        <span v-else-if="preparingMore" class="speech-player__hint">合成后续段落… 第 {{ synthesisProgress }} 段</span>
      </template>
    </AudioPlayer>
  </div>
</template>

<script setup lang="ts">
import { computed, onScopeDispose, watch } from 'vue'
import { AudioPlayer } from '@shared/web'
import { useSpeechPlayback } from '../composables/useSpeechPlayback'
import { splitSubtitleText } from '@/utils/speechText'

const props = defineProps<{
  text: string
  autoPlay?: boolean
  /** 点名页：优先用已有音频秒开，没有整段缓存时回退到正常播放 */
  playCachedOnly?: boolean
  /** 会议 id：用于读取服务端缓存的整段 wav */
  meetingId?: string
}>()
const emit = defineEmits<{
  currentSegment: [text: string]
}>()

const {
  playing,
  synthesizing,
  preparingMore,
  synthesisProgress,
  progress,
  currentTime,
  duration,
  play,
  playCached,
  stop,
} = useSpeechPlayback()
const synthesizingHint = computed(() => (
  synthesisProgress.value ? `正在生成语音… 第 ${synthesisProgress.value} 段` : '正在生成语音…'
))
const segments = computed(() => splitSubtitleText(props.text))
const segmentDurations = computed(() =>
  segments.value.map((s) => Math.max(0.5, s.replace(/\s/g, '').length / 4)),
)

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
      void startPlayback()
    }
  },
}))
// 把当前正在朗读的句子同步出去，供页面在视频顶部显示字幕
watch(
  [() => playing.value, () => currentTime.value],
  () => {
    if (!playing.value) {
      emit('currentSegment', '')
      return
    }
    let acc = 0
    let index = 0
    for (let i = 0; i < segmentDurations.value.length; i++) {
      acc += segmentDurations.value[i]!
      if (currentTime.value < acc) {
        index = i
        break
      }
      index = i
    }
    emit('currentSegment', segments.value[index] ?? '')
  },
  { immediate: true },
)

watch(
  () => [props.text, props.autoPlay] as const,
  ([text, autoPlay]) => {
    if (text && autoPlay) {
      void startPlayback()
    }
  },
  { immediate: true },
)

async function startPlayback(): Promise<void> {
  if (!props.text) return
  if (props.playCachedOnly) {
    await playCached(props.text, props.meetingId)
    return
  }
  void play(props.text, props.meetingId)
}

onScopeDispose(() => stop())

defineExpose({ stop })
</script>

<style scoped lang="less">
@import '@shared/web/styles/variables.less';

.speech-player {
  width: 100%;
}
.speech-player__hint {
  font-size: @font-size-xs;
  color: @text-tertiary;
  white-space: nowrap;
}
</style>
