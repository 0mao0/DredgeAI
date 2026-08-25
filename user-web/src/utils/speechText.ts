/**
 * 晨会稿分段：首段提前到第一个句尾，后续按 <=maxChars 字合并，供逐段 TTS 边播边合成。
 */
export function splitSpeechText(text: string, maxChars = 60): string[] {
  const normalized = text.replace(/\s+/g, ' ').trim()
  if (!normalized) return []

  const lead = extractLeadSegment(normalized)
  if (lead) {
    const rest = normalized.slice(lead.length).trim()
    const tail = splitIntoMax(rest, maxChars)
    return rest ? [lead, ...tail] : [lead]
  }

  return splitIntoMax(normalized, maxChars)
}

/** 字幕单行最大字数：22px 字号下一行放 14 个字符不会出省略号。 */
export const SUBTITLE_MAX_CHARS = 14

/**
 * 字幕分段：每个字幕段都是完整内容、不加省略号；装不下就自然拆到下一句。
 * 日期（如“今天是2026年8月25日”）会作为独立的一句，不会被切开。
 */
export function splitSubtitleText(text: string, maxChars = SUBTITLE_MAX_CHARS): string[] {
  const normalized = text.replace(/\s+/g, ' ').trim()
  if (!normalized) return []

  const parts = splitAtomicParts(normalized)

  const segments: string[] = []
  let buffer = ''
  for (const part of parts) {
    if (part.length > maxChars) {
      if (buffer) {
        segments.push(buffer)
        buffer = ''
      }
      for (const chunk of splitLongPart(part, maxChars)) {
        if (buffer && buffer.length + chunk.length > maxChars) {
          segments.push(buffer)
          buffer = ''
        }
        buffer += chunk
      }
      continue
    }
    if (buffer && buffer.length + part.length > maxChars) {
      segments.push(buffer)
      buffer = ''
    }
    buffer += part
  }
  if (buffer) segments.push(buffer)
  return segments
}

/** 按“完整句”和“日期”切出原子片段：句子结束或日期结束时断开，保证日期不被拆开。 */
function splitAtomicParts(text: string): string[] {
  const parts: string[] = []
  let buffer = ''
  const DATE_END = /\d{4}年\d{1,2}月\d{1,2}日$/
  const SENTENCE_END = /[。！？；;!?\n]$/
  for (const char of text) {
    buffer += char
    if (DATE_END.test(buffer) || SENTENCE_END.test(buffer)) {
      const part = buffer.trim()
      if (part) parts.push(part)
      buffer = ''
    }
  }
  const rest = buffer.trim()
  if (rest) parts.push(rest)
  return parts
}

function splitLongPart(part: string, maxChars: number): string[] {
  const commaChunks = part.split(/(?<=[，、])/).filter(Boolean)
  const chunks: string[] = []
  for (const chunk of commaChunks) {
    if (chunk.length <= maxChars) {
      chunks.push(chunk)
      continue
    }
    for (let i = 0; i < chunk.length; i += maxChars) {
      chunks.push(chunk.slice(i, i + maxChars))
    }
  }
  return chunks
}

/**
 * 首段只保留第一个句尾（尽量不超过 18 字），让用户点击后更快听到声音。
 */
function extractLeadSegment(text: string): string | null {
  const match = /[。！？；\n]/.exec(text)
  if (!match || match.index === undefined) return null
  const end = match.index + match[0].length
  if (end > 18) return null
  return text.slice(0, end)
}

function splitIntoMax(text: string, maxChars: number): string[] {
  const parts = text
    .split(/(?<=[。；;！？!?\n])/)
    .map((s) => s.trim())
    .filter(Boolean)

  const segments: string[] = []
  let buffer = ''
  for (const part of parts) {
    if (buffer && buffer.length + part.length > maxChars) {
      segments.push(buffer)
      buffer = part
    } else {
      buffer += part
    }
  }
  if (buffer) segments.push(buffer)
  return segments
}
