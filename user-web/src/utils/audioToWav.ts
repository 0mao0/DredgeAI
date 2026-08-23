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

function encodeWav(samples: Float32Array, sampleRate: number): Blob {
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
