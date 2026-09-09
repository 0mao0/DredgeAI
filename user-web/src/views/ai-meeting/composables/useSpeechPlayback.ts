import { ref } from 'vue'
import {
  getSpeechAudio,
  getSpeechLeadAudio,
  getSpeechAudioStatus,
  getSpeechSegmentAudio,
  saveSpeechAudioCache,
  streamSpeechAudio,
} from '@/api/modules/aiMeeting'
import { splitSpeechText } from '@/utils/speechText'
import { mergeAudioBlobs, parseWav, pcmToWavBlob } from '@/utils/audioToWav'
import { getPreparedSpeech, putPreparedSpeech } from './speechPreparedCache'
import { useAudioPlayer } from './useAudioPlayer'

/** 估算语速：约 4 字/秒 */
function estimateSeconds(text: string): number {
  return Math.round(text.replace(/\s/g, '').length / 4)
}

/**
 * 兜底分段合成的并发度：DGX 并发能力弱（闸门 3，且会拖慢单请求），
 * 这里严格串行——同一时刻只发一个 TTS 请求，避免和实时流/其他播放抢算力。
 */
const SEGMENT_CONCURRENCY = 1
/** 单句合成/拉取超时 */
const SEGMENT_FETCH_TIMEOUT = 30_000
/** 整稿流式播放的首块预缓冲：攒够这么多秒音频就开播（越小出声越快） */
const STREAM_PREBUFFER_SECONDS = 0.25
/**
 * 开播后每次调度的音频块时长。
 * 必须满足 预缓冲 ≥ 分块 / 上游吞吐（实测 1.8～2.2x），否则第二块到达前会断音——
 * 旧参数 0.5s 预缓冲配 1.5s 分块在 1.85x 下缺口约 0.3s。
 */
const STREAM_BLOCK_SECONDS = 0.4

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

  /**
   * 解析 16bit PCM WAV 为 AudioBuffer：按 WAV 头声明的原生采样率直接建 buffer
   * （与 DGX 官方测试页一致）。不用 decodeAudioData —— 逐帧 decode 会对每个小帧
   * 单独重采样（23040→上下文采样率），帧边界产生可闻的咔哒/滋滋声（约每 2s 一句停顿处最响）。
   */
  async function wavToAudioBuffer(ac: AudioContext, blob: Blob): Promise<AudioBuffer> {
    const wav = await parseWav(blob)
    const buffer = ac.createBuffer(wav.channels, wav.channelsData[0]!.length, wav.sampleRate)
    for (let ch = 0; ch < wav.channels; ch++) buffer.getChannelData(ch)!.set(wav.channelsData[ch]!)
    return buffer
  }

  /** 解析 16bit PCM 为 AudioBuffer：按声明采样率直接建 buffer，不做重采样（调用方须偶数对齐）。 */
  function pcmToAudioBuffer(ac: AudioContext, bytes: Uint8Array, sampleRate: number): AudioBuffer {
    const n = Math.floor(bytes.length / 2)
    const buffer = ac.createBuffer(1, n, sampleRate)
    const data = buffer.getChannelData(0)
    const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength)
    for (let i = 0; i < n; i++) data[i] = view.getInt16(i * 2, true) / 32768
    return buffer
  }

  /**
   * 单段合成：走流式接口收齐后包成 WAV。
   * 不再使用非流式 /tts 接口——上游同步整段合成在并发紧张时会拖慢甚至失败，统一走流式链路。
   */
  async function fetchSegmentAudio(text: string, timeoutMs: number): Promise<Blob> {
    const controller = new AbortController()
    const timer = window.setTimeout(() => controller.abort(), timeoutMs)
    let sampleRate = 24000
    const chunks: Uint8Array[] = []
    let total = 0
    try {
      for await (const chunk of streamSpeechAudio(text, (meta) => {
        sampleRate = meta.sampleRate
      }, controller.signal)) {
        chunks.push(chunk)
        total += chunk.length
      }
    } finally {
      window.clearTimeout(timer)
    }
    const evenLen = total - (total % 2)
    if (evenLen < 2) {
      throw new Error('单段合成为空')
    }
    const pcm = new Uint8Array(evenLen)
    let pos = 0
    for (const chunk of chunks) {
      if (pos >= evenLen) break
      const take = Math.min(chunk.length, evenLen - pos)
      pcm.set(chunk.subarray(0, take), pos)
      pos += take
    }
    return pcmToWavBlob(pcm, sampleRate)
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
   * 整稿流式播放：一次请求、上游按句吐 PCM，边到边无缝调度。
   * - 开场句有服务端缓存时先秒播，流式只合成剩余部分；
   * - 攒够 STREAM_PREBUFFER_SECONDS 才开播，之后按 STREAM_BLOCK_SECONDS 分块排期；
   * - 返回 ok=false 表示流式不可用（调用方回退逐段合成）。
   */
  async function tryStreamPlay(
    text: string,
    leadBlob: Blob | null | undefined,
    leadText: string,
    token: number,
  ): Promise<{ ok: boolean, merged: Blob | null }> {
    const ac = getAudioCtx()
    const lead = leadBlob && leadText && text.startsWith(leadText) ? leadText : ''
    const tailText = lead ? text.slice(lead.length).trim() : text
    const pcmChunks: Uint8Array[] = []
    let pending = new Uint8Array(0)
    let sampleRate = 24000
    let scheduledAny = false
    let lastScheduled = -1
    let tailChunks = 0
    let completed = false
    let index = 0

    const concat = (a: Uint8Array, b: Uint8Array): Uint8Array => {
      const out = new Uint8Array(a.length + b.length)
      out.set(a, 0)
      out.set(b, a.length)
      return out
    }

    const scheduleBuffer = (buffer: AudioBuffer): void => {
      if (token !== seq) return
      const source = ac.createBufferSource()
      source.buffer = buffer
      source.connect(ac.destination)
      const startAt = Math.max(streamNextStart, ac.currentTime + 0.02)
      source.start(startAt)
      streamNextStart = startAt + buffer.duration
      streamSources.push(source)
      const myIndex = index++
      lastScheduled = myIndex
      source.onended = () => {
        if (myIndex === lastScheduled) streamResolve?.()
      }
    }

    try {
      if (lead && leadBlob) {
        const buffer = await wavToAudioBuffer(ac, leadBlob)
        if (buffer.length > 0) scheduleBuffer(buffer)
        scheduledAny = true
        synthesizing.value = false
      }
      if (tailText) {
        for await (const chunk of streamSpeechAudio(tailText, (meta) => {
          sampleRate = meta.sampleRate
        })) {
          if (token !== seq) return { ok: true, merged: null }
          tailChunks++
          pending = concat(pending, chunk)
          const evenLen = pending.length - (pending.length % 2)
          const need = Math.floor((scheduledAny ? STREAM_BLOCK_SECONDS : STREAM_PREBUFFER_SECONDS) * sampleRate) * 2
          if (evenLen >= need) {
            const pcm = pending.slice(0, evenLen)
            pending = pending.slice(evenLen)
            pcmChunks.push(pcm)
            scheduleBuffer(pcmToAudioBuffer(ac, pcm, sampleRate))
            if (!scheduledAny) {
              scheduledAny = true
              synthesizing.value = false
              preparingMore.value = false
              synthesisProgress.value = ''
            }
          }
        }
        // 流结束时把不足一块的余量也排上
        const restLen = pending.length - (pending.length % 2)
        if (restLen >= 2) {
          const rest = pending.slice(0, restLen)
          pending = new Uint8Array(0)
          pcmChunks.push(rest)
          scheduleBuffer(pcmToAudioBuffer(ac, rest, sampleRate))
        }
      }
      completed = true
    } catch {
      // 流式接口不可用/中途中断 → 交给逐段回退
    }

    // 一句都没播出来（或剩余部分一个字节都没收到）→ 回退
    if (!scheduledAny || (tailText.length > 0 && tailChunks === 0)) {
      return { ok: false, merged: null }
    }
    // 中途中断：保留已播内容，不写回缓存（避免缓存半截音频）
    if (!completed) {
      return { ok: true, merged: null }
    }
    if (pcmChunks.length === 0) {
      return { ok: true, merged: null }
    }
    try {
      const total = pcmChunks.reduce((sum, c) => sum + c.length, 0)
      const mergedPcm = new Uint8Array(total)
      let pos = 0
      for (const c of pcmChunks) {
        mergedPcm.set(c, pos)
        pos += c.length
      }
      const tailBlob = pcmToWavBlob(mergedPcm, sampleRate)
      const merged = lead && leadBlob
        ? await mergeAudioBlobs([leadBlob, tailBlob])
        : tailBlob
      putPreparedSpeech(text, merged)
      streamMergedBlob = merged
      return { ok: true, merged }
    } catch {
      return { ok: true, merged: null }
    }
  }

  async function playStreamed(
    text: string,
    meetingId?: string,
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
    streamNextStart = ac.currentTime + 0.05
    streamMergedBlob = null

    let finished = false
    let allDone = Promise.resolve()
    const makeAllDone = (): void => {
      finished = false
      allDone = new Promise<void>((resolve) => {
        streamResolve = () => {
          if (!finished) {
            finished = true
            resolve()
          }
        }
      })
    }
    makeAllDone()
    timer = window.setInterval(() => {
      if (token !== seq) return
      currentTime.value = Math.min(currentTime.value + 1, duration.value)
      progress.value = duration.value > 0 ? currentTime.value / duration.value : 0
    }, 1000)

    // 整稿流式优先：一次请求、上游按句吐帧，首音最快且全程无缝
    const streamResult = await tryStreamPlay(text, leadBlob, leadText, token)
    if (streamResult.ok) {
      // 下载已完整（不是中途取消）：立刻写回服务端缓存，不必等尾部播完
      if (streamResult.merged && meetingId) {
        void saveSpeechAudioCache(meetingId, streamResult.merged).catch(() => {})
      }
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
      return streamResult.merged
    }

    // —— 回退：逐段合成（首段好就开播，后续并行合成，边播边产）——
    stopAudio()
    cancelStreaming()
    streamSources = []
    streamNextStart = ac.currentTime + 0.05
    streamMergedBlob = null
    synthesizing.value = true
    makeAllDone()

    function isLead(i: number): boolean {
      return i === 0 && !!leadBlob && !!leadText && segments[0] === leadText
    }

    async function fetchCached(index: number): Promise<Blob | null> {
      if (!meetingId) return null
      try {
        const blob = await getSpeechSegmentAudio(meetingId, index)
        return blob && blob.size > 0 ? blob : null
      } catch {
        // 未缓存（服务端分段预热未启用）→ 直接合成
        return null
      }
    }

    async function produceOne(i: number): Promise<Blob | null> {
      if (isLead(i)) return leadBlob!
      const cached = await fetchCached(i)
      if (cached) return cached
      for (let attempt = 0; attempt < 3; attempt++) {
        if (token !== seq) return null
        try {
          return await fetchSegmentAudio(segments[i]!, SEGMENT_FETCH_TIMEOUT)
        } catch {
          // 撞上游并发（429）时退避后重试
          if (attempt < 2) await new Promise((r) => setTimeout(r, 400 * (attempt + 1)))
        }
      }
      return null
    }

    const results = Array.from<Blob | null>({ length: total }).fill(null)
    const pending = Array.from<Promise<void>>({ length: total }).fill(Promise.resolve())
    let nextToStart = 0
    let inFlight = 0

    function kick(): void {
      while (nextToStart < total && inFlight < SEGMENT_CONCURRENCY) {
        const i = nextToStart++
        inFlight++
        pending[i] = produceOne(i)
          .then((blob) => {
            results[i] = blob
            if (token === seq) {
              synthesisProgress.value = `${Math.min(nextToStart, total)}/${total}`
            }
          })
          .finally(() => {
            inFlight--
            if (token === seq) kick()
          })
      }
    }
    kick()

    const pipeline = (async () => {
      const produced: Blob[] = []
      let lastScheduled = -1
      for (let i = 0; i < total; i++) {
        if (token !== seq) return
        await pending[i]!
        if (token !== seq) return
        const blob = results[i]
        synthesisProgress.value = `${Math.min(i + 1, total)}/${total}`
        if (!blob) continue
        produced.push(blob)
        if (i === 0) {
          synthesizing.value = false
          preparingMore.value = segments.length > 1
        }
        try {
          const buffer = await wavToAudioBuffer(ac, blob)
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
          putPreparedSpeech(text, merged)
          streamMergedBlob = merged
          if (meetingId) void saveSpeechAudioCache(meetingId, merged).catch(() => {})
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
    const cached = getPreparedSpeech(text)
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
        blobs.push(await fetchSegmentAudio(segment, SEGMENT_FETCH_TIMEOUT))
      }
      if (token !== seq) return false
      const merged = await mergeAudioBlobs(blobs)
      if (token !== seq) return false
      preparedText = text
      preparedBlob = merged
      putPreparedSpeech(text, merged)
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
    const cached = getPreparedSpeech(text)
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
        putPreparedSpeech(text, blob)
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

  async function playPreparedBlob(text: string, blob = preparedBlob): Promise<void> {
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
      const cached: Blob | null | undefined = getPreparedSpeech(text)
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
              putPreparedSpeech(text, blob)
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
          // 缓存未命中 → 流水线流式播放（首段即出、无缝衔接）；
          // 服务端缓存在音频收齐/合并完成时由 playStreamed 写回
          await playStreamed(text, meetingId, leadBlob, leadText)
          return
        }
      }
    }
    await playPreparedBlob(text)
  }

  /**
   * 点名页播放：优先用已有音频秒开（会话缓存 / 服务端整段 wav）；
   * 没有整段缓存时回退到正常播放——开场句缓存先秒出，剩余走实时流合成一次，
   * 播完写回整段缓存，下次进来就是秒开。
   */
  async function playCached(text: string, meetingId?: string): Promise<boolean> {
    if (preparedText !== text || !preparedBlob) {
      const cached = getPreparedSpeech(text)
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
            putPreparedSpeech(text, blob)
            duration.value = estimateSeconds(text)
            currentTime.value = 0
            progress.value = 0
            ready.value = true
          }
        } catch {
          // 状态查询失败按未缓存处理
        }
      }
      if (preparedText !== text || !preparedBlob) {
        // 没有整段缓存（服务端分段预热未启用）：正常播放，避免只播开场句就停
        void play(text, meetingId)
        return true
      }
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
