import { ref } from 'vue'
import { synthesizeSpeech } from '@/api/modules/aiMeeting'
import { splitSpeechText } from '@/utils/speechText'
import { useAudioPlayer } from './useAudioPlayer'

/** 估算语速：约 4 字/秒 */
function estimateSeconds(text: string): number {
  return Math.round(text.replace(/\s/g, '').length / 4)
}

/**
 * 晨会稿播放：分段 TTS 边播边合成 + 进度/时间估算。
 * 供晨会稿试听、点名自动播稿、会议念稿复用。
 */
export function useSpeechPlayback() {
  const { playOnce, stop: stopAudio } = useAudioPlayer()
  const playing = ref(false)
  const synthesizing = ref(false)
  const progress = ref(0)
  const currentTime = ref(0)
  const duration = ref(0)
  let seq = 0
  let timer: number | null = null

  async function play(text: string): Promise<void> {
    const segments = splitSpeechText(text)
    if (segments.length === 0) return
    const token = ++seq
    duration.value = estimateSeconds(text)
    currentTime.value = 0
    progress.value = 0
    playing.value = true
    synthesizing.value = true
    let playedSeconds = 0
    timer = window.setInterval(() => {
      if (token !== seq) return
      currentTime.value = Math.min(playedSeconds + 1, duration.value)
    }, 1000)

    try {
      let next = synthesizeSpeech(segments[0]!)
      for (let i = 0; i < segments.length; i++) {
        if (token !== seq) return
        const blob = await next
        if (i + 1 < segments.length) next = synthesizeSpeech(segments[i + 1]!)
        if (i === 0) synthesizing.value = false
        await playOnce(blob)
        playedSeconds += estimateSeconds(segments[i]!)
        progress.value = (i + 1) / segments.length
      }
    } catch {
      // 合成失败静默停止，调用方可通过 playing 状态感知
    } finally {
      if (token === seq) {
        playing.value = false
        synthesizing.value = false
        currentTime.value = duration.value
        if (timer) window.clearInterval(timer)
      }
    }
  }

  function stop(): void {
    seq++
    stopAudio()
    playing.value = false
    synthesizing.value = false
    currentTime.value = 0
    progress.value = 0
    if (timer) window.clearInterval(timer)
  }

  return { playing, synthesizing, progress, currentTime, duration, play, stop }
}
