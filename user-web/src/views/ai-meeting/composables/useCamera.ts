import { ref } from 'vue'

export function useCamera() {
  const stream = ref<MediaStream | null>(null)
  const error = ref<string | null>(null)

  async function start(): Promise<void> {
    try {
      stream.value = await navigator.mediaDevices.getUserMedia({ video: true, audio: false })
    } catch (e) {
      error.value = e instanceof Error ? e.message : '无法访问摄像头'
    }
  }

  function stop(): void {
    stream.value?.getTracks().forEach((t) => t.stop())
    stream.value = null
  }

  async function capturePhoto(video: HTMLVideoElement): Promise<Blob> {
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

  return { stream, error, start, stop, capturePhoto }
}
