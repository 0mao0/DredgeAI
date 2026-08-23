import { describe, it, expect, vi, beforeEach } from 'vitest'
import { useRecorder } from '@/views/ai-meeting/composables/useRecorder'
import { useAudioPlayer } from '@/views/ai-meeting/composables/useAudioPlayer'

class FakeMediaRecorder {
  ondataavailable: ((e: { data: Blob }) => void) | null = null
  onstop: (() => void) | null = null
  start(): void {}
  stop(): void {
    this.onstop?.()
  }
}

beforeEach(() => {
  Object.defineProperty(navigator, 'mediaDevices', {
    value: { getUserMedia: vi.fn().mockResolvedValue({ getTracks: () => [] }) },
    configurable: true,
  })
  vi.stubGlobal('MediaRecorder', FakeMediaRecorder)
})

describe('useRecorder', () => {
  it('start 后 recording 为 true，stop 后为 false', async () => {
    const rec = useRecorder()
    await rec.start()
    expect(rec.recording.value).toBe(true)
    await rec.stop()
    expect(rec.recording.value).toBe(false)
  })
})

describe('useAudioPlayer', () => {
  it('play 后 playing 为 true', () => {
    class FakeAudio {
      src = ''
      onended: (() => void) | null = null
      play = vi.fn()
      pause = vi.fn()
      currentTime = 0
    }
    vi.stubGlobal('Audio', FakeAudio)
    vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:fake'), revokeObjectURL: vi.fn() })
    const player = useAudioPlayer()
    player.play(new Blob(['x']))
    expect(player.playing.value).toBe(true)
  })

  it('play 被浏览器自动播放策略拦截时 playing 回到 false', async () => {
    class FakeAudio {
      src = ''
      onended: (() => void) | null = null
      play = vi.fn(() => Promise.reject(new Error('NotAllowedError')))
      pause = vi.fn()
      currentTime = 0
    }
    vi.stubGlobal('Audio', FakeAudio)
    vi.stubGlobal('URL', { createObjectURL: vi.fn(() => 'blob:fake'), revokeObjectURL: vi.fn() })
    const player = useAudioPlayer()

    player.play(new Blob(['x']))

    await vi.waitFor(() => expect(player.playing.value).toBe(false))
  })
})
