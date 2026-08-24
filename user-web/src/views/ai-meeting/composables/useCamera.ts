import { ref } from 'vue'

export function useCamera() {
  const stream = ref<MediaStream | null>(null)
  const error = ref<string | null>(null)
  const starting = ref(false)

  async function start(): Promise<boolean> {
    if (stream.value) return true
    starting.value = true
    error.value = null
    try {
      stream.value = await navigator.mediaDevices.getUserMedia({ video: true, audio: false })
      return true
    } catch (e) {
      error.value = e instanceof DOMException
        ? (e.name === 'NotAllowedError' ? '摄像头权限被拒绝，请在浏览器地址栏允许后重试' : e.message)
        : (e instanceof Error ? e.message : '无法访问摄像头')
      return false
    } finally {
      starting.value = false
    }
  }

  function stop(): void {
    stream.value?.getTracks().forEach((t) => t.stop())
    stream.value = null
  }

  async function capturePhoto(video: HTMLVideoElement): Promise<Blob> {
    if (!video.videoWidth || !video.videoHeight) {
      throw new Error('摄像头画面未就绪，请稍候重试')
    }
    const canvas = document.createElement('canvas')
    canvas.width = video.videoWidth
    canvas.height = video.videoHeight
    const ctx = canvas.getContext('2d')
    if (!ctx) throw new Error('canvas 不可用')
    ctx.drawImage(video, 0, 0)
    return new Promise<Blob>((resolve, reject) =>
      canvas.toBlob((b) => (b ? resolve(b) : reject(new Error('截图失败'))), 'image/jpeg', 0.92),
    )
  }

  return { stream, error, starting, start, stop, capturePhoto }
}
