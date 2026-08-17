import type { ChatDoneEvent, ChatRequest, ChatResult } from '@shared/core/types/chat'

export interface ChatStreamHandlers {
  onDelta: (text: string) => void
  onDone: (event: ChatDoneEvent) => void
  onFailed: (text: string, error: { type: string, message: string }) => void
  onError: (error: { type: string, message: string }) => void
}

export interface ChatTransport {
  chat: (req: ChatRequest) => Promise<ChatResult>
  chatStream: (req: ChatRequest, handlers: ChatStreamHandlers, signal?: AbortSignal) => Promise<void>
}

function parseEvent(line: string): ChatDoneEvent | { type: string, [k: string]: unknown } | null {
  const trimmed = line.trim()
  if (!trimmed.startsWith('data: ')) return null
  return JSON.parse(trimmed.slice(6))
}

export function createChatTransport(baseUrl = '/api/ai-gateway/chat/stream'): ChatTransport {
  return {
    async chat(req: ChatRequest): Promise<ChatResult> {
      const response = await fetch(baseUrl.replace('/chat/stream', '/chat'), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
      })
      if (!response.ok) throw new Error(`chat failed: ${response.status}`)
      return await response.json() as ChatResult
    },

    async chatStream(req, handlers, signal) {
      const response = await fetch(baseUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(req),
        signal,
      })
      if (!response.ok || !response.body) {
        handlers.onError({ type: 'HTTP_ERROR', message: `stream failed: ${response.status}` })
        return
      }

      const reader = response.body.getReader()
      const decoder = new TextDecoder()
      let buffer = ''
      for (;;) {
        const { done, value } = await reader.read()
        if (done) break
        buffer += decoder.decode(value, { stream: true })
        const lines = buffer.split('\n')
        buffer = lines.pop() ?? ''
        for (const line of lines) {
          const event = parseEvent(line)
          if (!event) continue
          switch (event.type) {
            case 'delta':
              handlers.onDelta(String(event.text ?? ''))
              break
            case 'done':
              handlers.onDone(event as ChatDoneEvent)
              break
            case 'stream_failed':
              handlers.onFailed(String(event.text ?? ''), event.error as { type: string, message: string })
              return
            case 'error':
              handlers.onError(event.error as { type: string, message: string })
              return
          }
        }
      }
    },
  }
}
