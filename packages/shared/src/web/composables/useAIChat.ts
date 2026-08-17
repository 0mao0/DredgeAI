import { ref } from 'vue'
import type { ChatMessage } from '@shared/core/types/chat'
import type { ChatTransport } from '@shared/web/chat/transport'

export function useAIChat(transport: ChatTransport, initial: ChatMessage[] = []) {
  const messages = ref<ChatMessage[]>([...initial])
  const loading = ref(false)
  const error = ref<string | null>(null)
  const streamingText = ref('')

  async function send(text: string): Promise<void> {
    const question = text.trim()
    if (!question || loading.value) return

    messages.value.push({ role: 'user', content: question })
    loading.value = true
    error.value = null
    streamingText.value = ''
    try {
      await transport.chatStream(
        {
          messages: [...messages.value],
          mode: 'instruct',
          business: 'standard-qa',
        },
        {
          onDelta: (delta) => { streamingText.value += delta },
          onDone: (event) => {
            messages.value.push({ role: 'assistant', content: event.text })
            streamingText.value = ''
          },
          onFailed: (text, err) => {
            if (text) messages.value.push({ role: 'assistant', content: text })
            error.value = err.message
          },
          onError: (err) => { error.value = err.message },
        },
      )
    } catch (e) {
      error.value = e instanceof Error ? e.message : '对话失败'
    } finally {
      loading.value = false
      streamingText.value = ''
    }
  }

  return { messages, loading, error, streamingText, send }
}
