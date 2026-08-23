import { ref } from 'vue'

export function useRecorder() {
  const stream = ref<MediaStream | null>(null)
  const recorder = ref<MediaRecorder | null>(null)
  const chunks = ref<Blob[]>([])
  const recording = ref(false)

  async function start(): Promise<void> {
    stream.value = await navigator.mediaDevices.getUserMedia({ audio: true, video: false })
    const rec = new MediaRecorder(stream.value)
    chunks.value = []
    rec.ondataavailable = (e) => {
      if (e.data.size > 0) chunks.value.push(e.data)
    }
    rec.start()
    recorder.value = rec
    recording.value = true
  }

  function stop(): Promise<Blob> {
    return new Promise((resolve) => {
      const rec = recorder.value
      if (!rec) {
        resolve(new Blob())
        return
      }
      rec.onstop = () => {
        stream.value?.getTracks().forEach((t) => t.stop())
        recording.value = false
        resolve(new Blob(chunks.value, { type: 'audio/webm' }))
      }
      rec.stop()
    })
  }

  return { recording, start, stop }
}
