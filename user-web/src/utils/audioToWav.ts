/**
 * 浏览器录音 → 16kHz 单声道 WAV。
 * 不同浏览器 MediaRecorder 产物不同（webm/opus、ogg、mp4/aac 等），
 * 统一转成标准 WAV 后上传，避免模型服务解码失败。
 */
export async function convertToWav16k(blob: Blob): Promise<Blob> {
  const arrayBuffer = await blob.arrayBuffer()
  const AudioCtx = window.AudioContext
    ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
  if (!AudioCtx) {
    throw new Error('当前浏览器不支持 Web Audio')
  }
  const ctx = new AudioCtx()
  try {
    const audioBuffer = await ctx.decodeAudioData(arrayBuffer)
    const sampleRate = 16000
    const offline = new OfflineAudioContext(1, Math.max(1, Math.ceil(audioBuffer.duration * sampleRate)), sampleRate)
    const source = offline.createBufferSource()
    source.buffer = audioBuffer
    source.connect(offline.destination)
    source.start(0)
    const rendered = await offline.startRendering()
    return encodeWav(rendered.getChannelData(0), sampleRate)
  } finally {
    void ctx.close()
  }
}

export interface ParsedWav {
  channels: number
  sampleRate: number
  /** 各声道 Float32 采样（归一化 -1..1） */
  channelsData: Float32Array[]
  /** data 块数据区在文件中的字节偏移 */
  dataOffset: number
}

/**
 * 解析 16bit PCM WAV（任意合法头：块遍历找 data）。
 * 用原生采样率直接还原采样，避免 decodeAudioData 逐帧重采样在帧边界产生可闻咔哒声。
 */
export async function parseWav(blob: Blob): Promise<ParsedWav> {
  const bytes = new Uint8Array(await blob.arrayBuffer())
  if (bytes.length < 44 || bytes[0] !== 0x52 || bytes[1] !== 0x49) {
    throw new Error('非 WAV 音频')
  }
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength)
  const channels = view.getUint16(22, true)
  const sampleRate = view.getUint32(24, true)
  const bits = view.getUint16(34, true)
  if (channels < 1 || channels > 2 || bits !== 16 || sampleRate <= 0) {
    throw new Error('不支持的 WAV 格式')
  }
  let dataOffset = -1
  let offset = 12
  while (offset + 8 <= bytes.length) {
    const id = String.fromCharCode(bytes[offset]!, bytes[offset + 1]!, bytes[offset + 2]!, bytes[offset + 3]!)
    const size = view.getUint32(offset + 4, true)
    if (id === 'data') {
      dataOffset = offset + 8
      break
    }
    offset += 8 + size + (size % 2)
  }
  if (dataOffset < 0 || bytes.length - dataOffset < channels * 2) {
    throw new Error('WAV 缺少 data 块')
  }
  const frameCount = Math.floor((bytes.length - dataOffset) / (channels * 2))
  const channelsData: Float32Array[] = []
  for (let ch = 0; ch < channels; ch++) {
    const out = new Float32Array(frameCount)
    const start = dataOffset + ch * 2
    for (let i = 0; i < frameCount; i++) {
      out[i] = view.getInt16(start + i * channels * 2, true) / 32768
    }
    channelsData.push(out)
  }
  return { channels, sampleRate, channelsData, dataOffset }
}

/**
 * 16bit PCM 原始字节直接包标准 WAV 头（不重采样、不转码）。
 * 用于流式收下的 PCM 块拼接后写回缓存/合并。
 */
export function pcmToWavBlob(pcmBytes: Uint8Array, sampleRate: number, channels = 1): Blob {
  const bytesPerSample = 2
  const dataSize = pcmBytes.length
  const buffer = new ArrayBuffer(44 + dataSize)
  const view = new DataView(buffer)
  const writeAscii = (offset: number, text: string): void => {
    for (let i = 0; i < text.length; i++) view.setUint8(offset + i, text.charCodeAt(i))
  }
  writeAscii(0, 'RIFF')
  view.setUint32(4, 36 + dataSize, true)
  writeAscii(8, 'WAVE')
  writeAscii(12, 'fmt ')
  view.setUint32(16, 16, true)
  view.setUint16(20, 1, true)
  view.setUint16(22, channels, true)
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, sampleRate * channels * bytesPerSample, true)
  view.setUint16(32, channels * bytesPerSample, true)
  view.setUint16(34, 16, true)
  writeAscii(36, 'data')
  view.setUint32(40, dataSize, true)
  new Uint8Array(buffer, 44).set(pcmBytes)
  return new Blob([buffer], { type: 'audio/wav' })
}

/**
 * 把多段音频（WAV 等）按顺序拼接为一段 WAV。
 * - 全部同采样率 16bit 单声道 → 字节级直拼（无损、零重采样开销）；
 * - 否则重采样渲染为 16kHz 单声道。
 * 供“先生成完整晨会稿录音、再一次性播放”使用，避免分段播放的割裂感。
 */
export async function mergeAudioBlobs(blobs: Blob[]): Promise<Blob> {
  if (blobs.length === 0) throw new Error('没有可合并的音频')
  if (blobs.length === 1) return blobs[0]!
  const parsed = await Promise.all(
    blobs.map(async (blob) => ({ wav: await parseWav(blob), bytes: new Uint8Array(await blob.arrayBuffer()) })),
  )
  const first = parsed[0]!
  const sameRateMono = parsed.every(({ wav }) => wav.sampleRate === first.wav.sampleRate && wav.channels === 1)
  if (sameRateMono) {
    let total = 0
    for (const { wav } of parsed) total += wav.channelsData[0]!.length * 2
    const merged = new Uint8Array(total)
    let pos = 0
    for (const { wav, bytes } of parsed) {
      const dataLen = wav.channelsData[0]!.length * 2
      merged.set(bytes.subarray(wav.dataOffset, wav.dataOffset + dataLen), pos)
      pos += dataLen
    }
    return pcmToWavBlob(merged, first.wav.sampleRate)
  }
  const AudioCtx = window.AudioContext
    ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
  if (!AudioCtx) throw new Error('当前浏览器不支持 Web Audio')
  const ctx = new AudioCtx()
  try {
    const sampleRate = 16000
    const sources: { buffer: AudioBuffer, duration: number }[] = []
    let totalSamples = 0
    for (const { wav } of parsed) {
      const buffer = ctx.createBuffer(wav.channels, wav.channelsData[0]!.length, wav.sampleRate)
      for (let ch = 0; ch < wav.channels; ch++) buffer.getChannelData(ch)!.set(wav.channelsData[ch]!)
      sources.push({ buffer, duration: buffer.duration })
      totalSamples += Math.max(1, Math.round(buffer.duration * sampleRate))
    }
    const offline = new OfflineAudioContext(1, totalSamples, sampleRate)
    let offset = 0
    for (const { buffer, duration } of sources) {
      const source = offline.createBufferSource()
      source.buffer = buffer
      source.connect(offline.destination)
      source.start(offset)
      offset += duration
    }
    const rendered = await offline.startRendering()
    return encodeWav(rendered.getChannelData(0), sampleRate)
  } finally {
    void ctx.close()
  }
}

const KNOWN_ERROR_MESSAGES: Record<string, string> = {
  MEETING_PLAN_EMPTY: '请先输入或说出今日计划',
  MEETING_PLAN_PARSE_FAILED: '未能从输入中识别出今日任务，请说得更完整一些',
  MEETING_PLAN_LLM_FAILED: '智能整理服务暂时不可用，请稍后重试',
  MEETING_BOT_CALL_FAILED: '语音服务暂时不可用，请稍后重试',
  AI_GATEWAY_FAILED: 'AI 服务暂时不可用，请稍后重试',
}

export function extractErrorMessage(err: unknown): string {
  const anyErr = err as {
    response?: { status?: number, data?: { error?: { code?: string, message?: string } } }
    message?: string
  } | null
  const code = anyErr?.response?.data?.error?.code
  if (code && KNOWN_ERROR_MESSAGES[code]) {
    return KNOWN_ERROR_MESSAGES[code]!
  }
  if (anyErr?.response?.data?.error?.message) {
    return anyErr.response.data.error.message
  }
  if (anyErr?.response?.status) {
    return `请求失败（HTTP ${anyErr.response.status}）`
  }
  if (anyErr?.message) {
    return anyErr.message
  }
  return '未知错误'
}

export function encodeWav(samples: Float32Array, sampleRate: number): Blob {
  const bytesPerSample = 2
  const dataSize = samples.length * bytesPerSample
  const buffer = new ArrayBuffer(44 + dataSize)
  const view = new DataView(buffer)
  const writeAscii = (offset: number, text: string): void => {
    for (let i = 0; i < text.length; i++) view.setUint8(offset + i, text.charCodeAt(i))
  }
  writeAscii(0, 'RIFF')
  view.setUint32(4, 36 + dataSize, true)
  writeAscii(8, 'WAVE')
  writeAscii(12, 'fmt ')
  view.setUint32(16, 16, true)
  view.setUint16(20, 1, true)
  view.setUint16(22, 1, true)
  view.setUint32(24, sampleRate, true)
  view.setUint32(28, sampleRate * bytesPerSample, true)
  view.setUint16(32, bytesPerSample, true)
  view.setUint16(34, 16, true)
  writeAscii(36, 'data')
  view.setUint32(40, dataSize, true)

  let offset = 44
  for (let i = 0; i < samples.length; i++) {
    const s = Math.max(-1, Math.min(1, samples[i]!))
    view.setInt16(offset, s < 0 ? s * 0x8000 : s * 0x7FFF, true)
    offset += bytesPerSample
  }
  return new Blob([buffer], { type: 'audio/wav' })
}
