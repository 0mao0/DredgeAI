import { describe, it, expect, vi, beforeEach } from 'vitest'

const streamCalls: string[] = []
const saveSpeechAudioCache = vi.fn(async () => {})
/** 每次 yield 后已到达的音频秒数，用于断言"首音不必等满整块" */
let pulledSeconds = 0
const startEvents: Array<{ pulledSeconds: number, duration: number }> = []

/** 分块闸门：gated=true 时每个分块都要测试显式放行，便于在中途停止播放 */
let gated = false
const gates: Array<() => void> = []

const flush = (): Promise<void> => new Promise((resolve) => setTimeout(resolve, 0))

/** 逐块放行：每放一块要等生成器重新挂上下一个闸门 */
async function releaseChunks(count: number): Promise<void> {
  for (let i = 0; i < count; i++) {
    gates.shift()?.()
    await flush()
  }
}

vi.mock('@/api/modules/aiMeeting', () => ({
  async* streamSpeechAudio(text: string, onMeta?: (m: { sampleRate: number }) => void) {
    streamCalls.push(text)
    onMeta?.({ sampleRate: 24000 })
    // 20 × 0.1s = 2s 的 24kHz 单声道 16bit PCM
    const chunk = new Uint8Array(24000 * 2 * 0.1)
    for (let i = 0; i < 20; i++) {
      if (gated) await new Promise<void>((resolve) => gates.push(resolve))
      pulledSeconds += 0.1
      yield chunk
    }
  },
  getSpeechAudioStatus: vi.fn(async () => ({ cached: false, leadCached: false, leadText: '' })),
  getSpeechAudio: vi.fn(async () => {
    throw new Error('不该走整段缓存分支')
  }),
  getSpeechLeadAudio: vi.fn(async () => {
    throw new Error('不该走开场句缓存分支')
  }),
  getSpeechSegmentAudio: vi.fn(async () => {
    throw new Error('不该走分段回退分支')
  }),
  saveSpeechAudioCache,
}))

/** 极简 Web Audio 替身：只覆盖 pcmToAudioBuffer / scheduleBuffer 用到的接口 */
class FakeAudioBuffer {
  duration: number
  private readonly data: Float32Array

  constructor(length: number, sampleRate: number) {
    this.duration = length / sampleRate
    this.data = new Float32Array(length)
  }

  getChannelData(): Float32Array {
    return this.data
  }
}

class FakeBufferSource {
  buffer: FakeAudioBuffer | null = null
  onended: (() => void) | null = null
  connect(): void {}
  start(): void {
    startEvents.push({ pulledSeconds, duration: this.buffer?.duration ?? 0 })
    setTimeout(() => this.onended?.(), 0)
  }

  stop(): void {}
}

class FakeAudioContext {
  currentTime = 0
  destination = {}
  createBuffer(_channels: number, length: number, sampleRate: number): FakeAudioBuffer {
    return new FakeAudioBuffer(length, sampleRate)
  }

  createBufferSource(): FakeBufferSource {
    return new FakeBufferSource()
  }

  async resume(): Promise<void> {}
}

/** useAudioPlayer 在 setup 阶段就 new Audio()，需要占位实现 */
class FakeAudio {
  src = ''
  currentTime = 0
  onended: (() => void) | null = null
  onerror: (() => void) | null = null
  constructor(src?: string) {
    if (src) this.src = src
  }

  play(): Promise<void> {
    return Promise.resolve()
  }

  pause(): void {}
}

if (!globalThis.window) {
  ;(globalThis as unknown as { window: unknown }).window = globalThis
}
vi.stubGlobal('AudioContext', FakeAudioContext)
vi.stubGlobal('Audio', FakeAudio)

beforeEach(() => {
  streamCalls.length = 0
  startEvents.length = 0
  gates.length = 0
  gated = false
  pulledSeconds = 0
  saveSpeechAudioCache.mockClear()
})

describe('useSpeechPlayback 整稿流式播放', () => {
  it('流式播放完整走完后写回服务端整段缓存（pcmChunks 必须累积）', async () => {
    const { useSpeechPlayback } = await import('@/views/ai-meeting/composables/useSpeechPlayback')
    const { play } = useSpeechPlayback()
    await play('各位工友，大家早上好！这是流式缓存回归用例。', 'meeting-regression-1')

    expect(streamCalls).toHaveLength(1)
    expect(saveSpeechAudioCache).toHaveBeenCalledTimes(1)
    const [id, blob] = saveSpeechAudioCache.mock.calls[0] as unknown as [string, Blob]
    expect(id).toBe('meeting-regression-1')
    // 2s × 24000Hz × 2 字节 + 44 字节 WAV 头
    expect(blob.size).toBe(24000 * 2 * 2 + 44)
  })

  it('首音不必等满一整块：到达 ≤0.4s 音频即开播', async () => {
    const { useSpeechPlayback } = await import('@/views/ai-meeting/composables/useSpeechPlayback')
    const { play } = useSpeechPlayback()
    await play('各位工友，大家早上好！首音延迟回归用例。', 'meeting-regression-2')

    expect(startEvents.length).toBeGreaterThan(0)
    expect(startEvents[0]!.pulledSeconds).toBeLessThanOrEqual(0.4)
    expect(startEvents[0]!.duration).toBeGreaterThan(0)
  })

  it('停止播放后不再排期，但合成任务继续收完并写回缓存', async () => {
    gated = true
    const { useSpeechPlayback } = await import('@/views/ai-meeting/composables/useSpeechPlayback')
    const { play, stop } = useSpeechPlayback()
    const playback = play('各位工友，大家早上好！停止后继续收完的用例。', 'meeting-drain-1')
    await flush()
    await releaseChunks(4)
    await flush()
    expect(startEvents.length).toBeGreaterThan(0)

    stop()
    const scheduledAtStop = startEvents.length
    await releaseChunks(30)
    await flush()
    await flush()
    await playback

    expect(streamCalls).toHaveLength(1)
    expect(startEvents.length).toBe(scheduledAtStop)
    expect(saveSpeechAudioCache).toHaveBeenCalledTimes(1)
    const [, blob] = saveSpeechAudioCache.mock.calls[0] as unknown as [string, Blob]
    expect(blob.size).toBe(24000 * 2 * 2 + 44)
  })

  it('同一文本再次播放复用进行中的任务，不重复请求上游', async () => {
    gated = true
    const { useSpeechPlayback } = await import('@/views/ai-meeting/composables/useSpeechPlayback')
    const playback = useSpeechPlayback()
    const first = playback.play('各位工友，大家早上好！复用进行中任务的用例。', 'meeting-join-1')
    await flush()
    await releaseChunks(3)
    await flush()
    playback.stop()
    await first

    const second = playback.play('各位工友，大家早上好！复用进行中任务的用例。', 'meeting-join-1')
    await flush()
    await releaseChunks(30)
    await flush()
    await flush()
    await second

    expect(streamCalls).toHaveLength(1)
    expect(saveSpeechAudioCache).toHaveBeenCalledTimes(1)
  })
})
