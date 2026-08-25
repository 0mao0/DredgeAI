import { ref } from 'vue'
import {
  getSpeechAudio,
  getSpeechLeadAudio,
  getSpeechAudioStatus,
  saveSpeechAudioCache,
  synthesizeSpeech,
} from '@/api/modules/aiMeeting'
import { splitSpeechText } from '@/utils/speechText'
import { mergeAudioBlobs } from '@/utils/audioToWav'
import { useAudioPlayer } from './useAudioPlayer'

/** 估算语速：约 4 字/秒 */
function estimateSeconds(text: string): number {
  return Math.round(text.replace(/\s/g, '').length / 4)
}

/** 跨实例共享已生成语音：晨会稿页生成后，点名页直接复用，避免重复合成 */
const sharedPrepared = new Map<string, Blob>()

/**
 * 晨会稿播放：
 * - 缓存命中（本会话已生成/服务端整段）→ 直接整段播放，秒出；
 * - 缓存未命中 → 流水线流式播放：首段合成好立即开播，后续段落边播边合成，
 *   Web Audio 精确调度做到段间零间隙。
 */
export function useSpeechPlayback() {
  const { playOnce, stop: stopAudio } = useAudioPlayer()
  const playing = ref(false)
  const synthesizing = ref(false)
  const ready = ref(false)
  /** 合成进度（第几段/共几段），供占位提示 */
  const synthesisProgress = ref('')
  /** 首段已开播、后续段落仍在后台合成 */
  const preparingMore = ref(false)
  const progress = ref(0)
  const currentTime = ref(0)
  const duration = ref(0)
  let seq = 0
  let timer: number | null = null
  let preparedText = ''
  let preparedBlob: Blob | null = null

  // —— 流式播放（Web Audio 精确调度，段间零间隙）——
  let audioCtx: AudioContext | null = null
  let streamSources: AudioBufferSourceNode[] = []
  let streamResolve: (() => void) | null = null
  let streamNextStart = 0
  let streamMergedBlob: Blob | null = null

  function getAudioCtx(): AudioContext {
    if (!audioCtx) audioCtx = new AudioContext()
    return audioCtx
  }

  function cancelStreaming(): void {
    for (const source of streamSources) {
      try {
        source.stop()
      } catch {
        // 已停止
      }
    }
    streamSources = []
    streamResolve?.()
    streamResolve = null
  }

  /**
   * 流水线流式播放：合成好第 1 段立即开播，后续段落边播边合成并预取，
   * 用 Web Audio 精确调度做到段与段零间隙。
   */
  /** 流水线流式播放，结束后返回合并好的整段音频（供写入服务端缓存），失败返回 null。 */
  async function playStreamed(
    text: string,
    leadBlob?: Blob | null,
    leadText = '',
  ): Promise<Blob | null> {
    const segments = splitSpeechText(text)
    if (segments.length === 0) return null
    const total = segments.length
    const token = ++seq
    duration.value = estimateSeconds(text)
    currentTime.value = 0
    progress.value = 0
    playing.value = true
    synthesizing.value = true
    preparingMore.value = false
    synthesisProgress.value = `0/${total}`
    stopAudio()
    cancelStreaming()

    const ac = getAudioCtx()
    try {
      await ac.resume()
    } catch {
      // 自动播放策略拦截时静默失败，等待用户手势
    }
    streamSources = []
    streamNextStart = ac.currentTime + 0.1
    streamMergedBlob = null
    let finished = false
    const allDone = new Promise<void>((resolve) => {
      streamResolve = () => {
        if (!finished) {
          finished = true
          resolve()
        }
      }
    })
    timer = window.setInterval(() => {
      if (token !== seq) return
      currentTime.value = Math.min(currentTime.value + 1, duration.value)
      progress.value = duration.value > 0 ? currentTime.value / duration.value : 0
    }, 1000)

    const pipeline = (async () => {
      const produced: Blob[] = []
      let lastScheduled = -1
      for (let i = 0; i < segments.length; i++) {
        if (token !== seq) return
        // 首段若已由服务端预合成缓存，直接复用，点击播放即可秒出
        let blob: Blob | null = null
        if (i === 0 && leadBlob && leadText && segments[0] === leadText) {
          blob = leadBlob
        } else {
          // 每段失败自动重试；仍失败则跳过该段，不中断整段播放
          for (let attempt = 0; attempt < 3 && blob === null; attempt++) {
            if (token !== seq) return
            try {
              blob = await synthesizeSpeech(segments[i]!, 30_000)
            } catch {
              blob = null
            }
          }
        }
        synthesisProgress.value = `${Math.min(i + 1, total)}/${total}`
        if (!blob) continue
        if (token !== seq) return
        produced.push(blob)
        if (i === 0) {
          synthesizing.value = false
          preparingMore.value = segments.length > 1
        }
        try {
          const buffer = await ac.decodeAudioData(await blob.arrayBuffer())
          if (token !== seq) return
          const source = ac.createBufferSource()
          source.buffer = buffer
          source.connect(ac.destination)
          const startAt = Math.max(streamNextStart, ac.currentTime + 0.02)
          source.start(startAt)
          streamNextStart = startAt + buffer.duration
          streamSources.push(source)
          lastScheduled = i
          source.onended = () => {
            if (i === lastScheduled) streamResolve?.()
          }
        } catch {
          // 单段解码失败跳过，继续下一段
        }
      }
      if (token === seq && produced.length > 0) {
        try {
          const merged = await mergeAudioBlobs(produced)
          sharedPrepared.set(text, merged)
          if (sharedPrepared.size > 10) {
            const oldest = sharedPrepared.keys().next().value
            if (oldest !== undefined) sharedPrepared.delete(oldest)
          }
          streamMergedBlob = merged
        } catch {
          // 合并失败不影响已播内容
        }
      }
      if (lastScheduled === -1 && token === seq) streamResolve?.()
    })()
    void pipeline

    await allDone
    if (token === seq) {
      playing.value = false
      synthesizing.value = false
      preparingMore.value = false
      synthesisProgress.value = ''
      currentTime.value = duration.value
      progress.value = 1
      if (timer) window.clearInterval(timer)
    }
    return streamMergedBlob
  }

  /** 先生成完整语音但不播放（生成完点击试听）；同一文本复用已生成音频。 */
  async function generate(text: string): Promise<boolean> {
    const segments = splitSpeechText(text)
    if (segments.length === 0) return false
    if (preparedText === text && preparedBlob) return true
    const cached = sharedPrepared.get(text)
    if (cached) {
      preparedText = text
      preparedBlob = cached
      duration.value = estimateSeconds(text)
      currentTime.value = 0
      progress.value = 0
      ready.value = true
      return true
    }
    const token = ++seq
    stopAudio()
    cancelStreaming()
    playing.value = false
    ready.value = false
    preparedBlob = null
    currentTime.value = 0
    progress.value = 0
    synthesizing.value = true
    try {
      const blobs: Blob[] = []
      for (const segment of segments) {
        if (token !== seq) return false
        blobs.push(await synthesizeSpeech(segment))
      }
      if (token !== seq) return false
      const merged = await mergeAudioBlobs(blobs)
      if (token !== seq) return false
      preparedText = text
      preparedBlob = merged
      sharedPrepared.set(text, merged)
      if (sharedPrepared.size > 10) {
        const oldest = sharedPrepared.keys().next().value
        if (oldest !== undefined) sharedPrepared.delete(oldest)
      }
      duration.value = estimateSeconds(text)
      ready.value = true
      return true
    } catch {
      return false
    } finally {
      if (token === seq) synthesizing.value = false
    }
  }

  /** 优先拉取服务端整段语音（带缓存），失败回退客户端逐段合成。 */
  async function ensure(text: string, fetcher?: () => Promise<Blob>): Promise<boolean> {
    if (preparedText === text && preparedBlob) return true
    const cached = sharedPrepared.get(text)
    if (cached) {
      preparedText = text
      preparedBlob = cached
      duration.value = estimateSeconds(text)
      currentTime.value = 0
      progress.value = 0
      ready.value = true
      return true
    }
    if (fetcher) {
      const token = ++seq
      stopAudio()
      cancelStreaming()
      playing.value = false
      ready.value = false
      preparedBlob = null
      currentTime.value = 0
      progress.value = 0
      synthesizing.value = true
      try {
        const blob = await fetcher()
        if (token !== seq) return false
        preparedText = text
        preparedBlob = blob
        sharedPrepared.set(text, blob)
        duration.value = estimateSeconds(text)
        ready.value = true
        return true
      } catch {
        // 服务端音频不可用，回退客户端合成
      } finally {
        if (token === seq) synthesizing.value = false
      }
    }
    return generate(text)
  }

  async function playPreparedBlob(text: string): Promise<void> {
    const blob = preparedBlob
    if (!blob) return
    const token = ++seq
    duration.value = estimateSeconds(text)
    currentTime.value = 0
    progress.value = 0
    playing.value = true
    timer = window.setInterval(() => {
      if (token !== seq) return
      currentTime.value = Math.min(currentTime.value + 1, duration.value)
      progress.value = duration.value > 0 ? currentTime.value / duration.value : 0
    }, 1000)
    try {
      await playOnce(blob)
      if (token === seq) progress.value = 1
    } catch {
      // 播放失败静默停止
    } finally {
      if (token === seq) {
        playing.value = false
        if (timer) window.clearInterval(timer)
        currentTime.value = duration.value
        progress.value = 1
      }
    }
  }

  async function play(text: string, meetingId?: string): Promise<void> {
    if (preparedText !== text || !preparedBlob) {
      const cached = sharedPrepared.get(text)
      if (cached) {
        preparedText = text
        preparedBlob = cached
        duration.value = estimateSeconds(text)
        currentTime.value = 0
        progress.value = 0
        ready.value = true
      } else {
        // 服务端已缓存整段 wav → 直接拉取整段播放（秒出）
        let leadBlob: Blob | null = null
        let leadText = ''
        if (meetingId) {
          try {
            const status = await getSpeechAudioStatus(meetingId)
            if (status.cached) {
              const blob = await getSpeechAudio(meetingId)
              preparedText = text
              preparedBlob = blob
              sharedPrepared.set(text, blob)
              duration.value = estimateSeconds(text)
              currentTime.value = 0
              progress.value = 0
              ready.value = true
            } else if (status.leadCached && status.leadText) {
              leadText = status.leadText
              leadBlob = await getSpeechLeadAudio(meetingId)
            }
          } catch {
            // 状态查询失败按未缓存处理，走流式
          }
        }
        if (preparedText !== text || !preparedBlob) {
          // 缓存未命中 → 流水线流式播放（首段即出、无缝衔接），播完写回服务端缓存
          const merged = await playStreamed(text, leadBlob, leadText)
          if (merged && meetingId) {
            void saveSpeechAudioCache(meetingId, merged).catch(() => {})
          }
          return
        }
      }
    }
    await playPreparedBlob(text)
  }

  /**
   * 仅播放已有音频（会话缓存或服务端 wav），不触发任何合成。
   * 返回是否成功开始播放（无缓存时返回 false，由调用方提示“语音尚未生成”）。
   */
  async function playCached(text: string, meetingId?: string): Promise<boolean> {
    if (preparedText !== text || !preparedBlob) {
      const cached = sharedPrepared.get(text)
      if (cached) {
        preparedText = text
        preparedBlob = cached
        duration.value = estimateSeconds(text)
        currentTime.value = 0
        progress.value = 0
        ready.value = true
      } else if (meetingId) {
        try {
          const status = await getSpeechAudioStatus(meetingId)
          if (status.cached) {
            const blob = await getSpeechAudio(meetingId)
            preparedText = text
            preparedBlob = blob
            sharedPrepared.set(text, blob)
            duration.value = estimateSeconds(text)
            currentTime.value = 0
            progress.value = 0
            ready.value = true
          }
        } catch {
          // 状态查询失败视为无缓存
        }
      }
      if (preparedText !== text || !preparedBlob) return false
    }
    await playPreparedBlob(text)
    return true
  }

  function stop(): void {
    seq++
    stopAudio()
    cancelStreaming()
    playing.value = false
    synthesizing.value = false
    preparingMore.value = false
    synthesisProgress.value = ''
    currentTime.value = 0
    progress.value = 0
    if (timer) window.clearInterval(timer)
  }

  return {
    playing,
    synthesizing,
    preparingMore,
    synthesisProgress,
    ready,
    progress,
    currentTime,
    duration,
    play,
    playCached,
    generate,
    ensure,
    playStreamed,
    stop,
  }
}
