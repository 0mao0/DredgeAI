import { ref } from 'vue'

export function useAudioPlayer() {
  const audio = new Audio()
  const playing = ref(false)
  let currentUrl: string | null = null
  let currentAudio: HTMLAudioElement | null = null
  let currentResolve: (() => void) | null = null
  let seqToken = 0

  function play(blob: Blob): void {
    stop()
    currentUrl = URL.createObjectURL(blob)
    audio.src = currentUrl
    playing.value = true
    const result = audio.play()
    // 浏览器自动播放策略拦截时（无用户手势）回落到静默失败态，按钮仍可手动重播
    if (result && typeof result.catch === 'function') {
      result.catch(() => {
        playing.value = false
      })
    }
    audio.onended = () => {
      playing.value = false
      if (currentUrl) URL.revokeObjectURL(currentUrl)
      currentUrl = null
    }
  }

  function stop(): void {
    seqToken++
    currentAudio?.pause()
    currentAudio = null
    currentResolve?.()
    currentResolve = null
    audio.pause()
    audio.currentTime = 0
    if (currentUrl) URL.revokeObjectURL(currentUrl)
    currentUrl = null
    playing.value = false
  }

  /**
   * 顺序播放单个音频，结束后 resolve；stop() 会立即中断并 resolve。
   * 供分段 TTS 边播边合成使用。
   */
  function playOnce(blob: Blob): Promise<void> {
    const token = ++seqToken
    stop()
    return new Promise((resolve) => {
      let settled = false
      const finish = (): void => {
        if (settled) return
        settled = true
        resolve()
      }
      const url = URL.createObjectURL(blob)
      const player = new Audio(url)
      currentAudio = player
      currentResolve = finish
      playing.value = true
      const done = (): void => {
        URL.revokeObjectURL(url)
        if (token === seqToken) playing.value = false
        finish()
      }
      player.onended = done
      player.onerror = done
      const result = player.play()
      if (result && typeof result.catch === 'function') result.catch(done)
    })
  }

  return { playing, play, stop, playOnce }
}
