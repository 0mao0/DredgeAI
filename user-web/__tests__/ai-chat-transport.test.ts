import { afterEach, describe, expect, it, vi } from 'vitest'
import { createChatTransport } from '@shared/web/chat/transport'

function sseResponse(...chunks: string[]): Response {
  const body = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const chunk of chunks) controller.enqueue(new TextEncoder().encode(chunk))
      controller.close()
    },
  })
  return new Response(body, { status: 200 })
}

afterEach(() => vi.unstubAllGlobals())

describe('createChatTransport.chatStream', () => {
  it('parses delta and done events', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(sseResponse(
      'data: {"type":"delta","text":"你"}\n\n',
      'data: {"type":"delta","text":"好"}\n\n',
      'data: {"type":"done","text":"你好","finishReason":"stop","attempts":1,"usedConfig":"fake","usedModel":"m","latencySeconds":0.1,"usage":{"total_tokens":5},"circuitBreakerState":"closed"}\n\n',
    )))

    const deltas: string[] = []
    let doneText = ''
    const transport = createChatTransport('/api/ai-gateway/chat/stream')
    await transport.chatStream(
      { messages: [{ role: 'user', content: 'hi' }] },
      {
        onDelta: (t) => deltas.push(t),
        onDone: (e) => { doneText = e.text },
        onFailed: () => {},
        onError: () => {},
      },
    )

    expect(deltas).toEqual(['你', '好'])
    expect(doneText).toBe('你好')
  })

  it('stops on stream_failed and keeps partial text', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(sseResponse(
      'data: {"type":"delta","text":"部分"}\n\n',
      'data: {"type":"stream_failed","text":"部分","error":{"type":"LLMStreamError","message":"中断"}}\n\n',
    )))

    let partial = ''
    let failed = false
    const transport = createChatTransport('/api/ai-gateway/chat/stream')
    await transport.chatStream(
      { messages: [{ role: 'user', content: 'hi' }] },
      {
        onDelta: (t) => { partial += t },
        onDone: () => {},
        onFailed: (text) => { failed = text === '部分' },
        onError: () => {},
      },
    )

    expect(partial).toBe('部分')
    expect(failed).toBe(true)
  })
})
