/**
 * 晨会稿整段语音的会话级共享缓存（按稿子全文 key）。
 * 从 useSpeechPlayback 抽出：文字流式预取与实时播放共用同一份缓存，
 * 命中即整段播放，避免重复合成。
 */
const MAX_ENTRIES = 10

const prepared = new Map<string, Blob>()

export function getPreparedSpeech(text: string): Blob | undefined {
  return prepared.get(text)
}

export function putPreparedSpeech(text: string, blob: Blob): void {
  prepared.delete(text)
  prepared.set(text, blob)
  while (prepared.size > MAX_ENTRIES) {
    const oldest = prepared.keys().next().value
    if (oldest === undefined) break
    prepared.delete(oldest)
  }
}
