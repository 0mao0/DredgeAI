export interface ApiResponse<T = unknown> {
  code: number
  data: T
  message: string
}

export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

export function randomDelay(): Promise<void> {
  return delay(200 + Math.floor(Math.random() * 200))
}
