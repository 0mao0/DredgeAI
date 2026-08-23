import { ref } from 'vue'

export function useAudioPlayer() {
  const audio = new Audio()
  const playing = ref(false)
  let currentUrl: string | null = null

  function play(blob: Blob): void {
    stop()
    currentUrl = URL.createObjectURL(blob)
    audio.src = currentUrl
    void audio.play()
    playing.value = true
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
