import { ref } from 'vue'

export interface RecorderStartOptions {
  /** MediaRecorder 分片间隔（ms），传入后每个分片通过 onChunk 回调 */
  timeslice?: number
  onChunk?: (chunk: Blob) => void
}

export function useRecorder() {
  const stream = ref<MediaStream | null>(null)
  const recorder = ref<MediaRecorder | null>(null)
  const chunks = ref<Blob[]>([])
  const recording = ref(false)
  const paused = ref(false)

  async function start(options?: RecorderStartOptions): Promise<void> {
    stream.value = await navigator.mediaDevices.getUserMedia({ audio: true, video: false })
    const rec = new MediaRecorder(stream.value)
    chunks.value = []
    paused.value = false
    rec.ondataavailable = (e) => {
      if (e.data.size > 0) {
        chunks.value.push(e.data)
        options?.onChunk?.(e.data)
      }
    }
    rec.onpause = () => {
      // Chromium 在 paused 状态调用 stop() 时会补发一次 pause 事件，
      // 此时 recorder 已 inactive，需按实际 state 判定，避免把已停止状态误标为暂停
      if (rec.state === 'paused') paused.value = true
    }
    rec.onresume = () => {
      paused.value = false
    }
    rec.onstop = () => {
      stream.value?.getTracks().forEach((t) => t.stop())
      recording.value = false
      paused.value = false
    }
    rec.start(options?.timeslice)
    recorder.value = rec
    recording.value = true
  }

  function pause(): void {
    if (recorder.value && recorder.value.state === 'recording') {
      recorder.value.pause()
    }
  }

  function resume(): void {
    if (recorder.value && recorder.value.state === 'paused') {
      recorder.value.resume()
    }
  }

  function stop(): Promise<Blob> {
    return new Promise((resolve) => {
      const rec = recorder.value
      if (!rec || rec.state === 'inactive') {
        resolve(new Blob())
        return
      }
      rec.onstop = () => {
        stream.value?.getTracks().forEach((t) => t.stop())
        recording.value = false
        paused.value = false
        resolve(new Blob(chunks.value, { type: 'audio/webm' }))
      }
      paused.value = false
      rec.stop()
    })
  }

  return { recording, paused, start, pause, resume, stop }
}
