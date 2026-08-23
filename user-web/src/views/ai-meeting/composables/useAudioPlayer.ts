import { ref } from 'vue'

export function useAudioPlayer() {
  const audio = new Audio()
  const playing = ref(false)
  let currentUrl: string | null = null

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
    audio.pause()
    audio.currentTime = 0
    if (currentUrl) URL.revokeObjectURL(currentUrl)
    currentUrl = null
    playing.value = false
  }

  return { playing, play, stop }
}
