export interface ChatMessage {
  role: 'system' | 'user' | 'assistant'
  content: string
}

export interface ChatRequest {
  messages: ChatMessage[]
  mode?: 'instruct' | 'thinking'
  configName?: string
  temperature?: number
  maxTokens?: number
  business?: string
}

export interface ChatUsage {
  prompt_tokens?: number
  completion_tokens?: number
  total_tokens?: number
}

export interface ChatResult {
  text: string
  finishReason: string | null
  usage: ChatUsage | null
  usedConfig: string | null
  usedModel: string | null
  attempts: number
  latencySeconds: number | null
  circuitBreakerState: string | null
}

export interface ChatDoneEvent extends ChatResult {
  type: 'done'
}

export interface ChatDeltaEvent {
  type: 'delta'
  text: string
}

export interface ChatStreamFailedEvent {
  type: 'stream_failed'
  text: string
  error: { type: string, message: string }
  usedConfig: string | null
  usedModel: string | null
  attempts: number
  latencySeconds: number | null
  circuitBreakerState: string | null
}

export interface ChatErrorEvent {
  type: 'error'
  error: { type: string, message: string }
}

export type ChatStreamEvent
  = | ChatDeltaEvent
    | ChatDoneEvent
    | ChatStreamFailedEvent
    | ChatErrorEvent
