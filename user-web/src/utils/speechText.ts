/**
 * 晨会稿分段：按句切分并合并到 <=maxChars 字，供逐段 TTS 边播边合成。
 */
export function splitSpeechText(text: string, maxChars = 40): string[] {
  const normalized = text.replace(/\s+/g, ' ').trim()
  if (!normalized) return []

  const parts = normalized
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
