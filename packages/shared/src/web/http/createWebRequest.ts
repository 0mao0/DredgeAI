import { createRequest } from '@shared/core/http'
import type { CreateRequestOptions, RequestInstance } from '@shared/core/http/types'

/**
 * 封装 createRequest 并注入请求进度与错误回调。
 * 各端可通过 onProgress / onError 参数注入具体实现（nprogress / message 等），
 * 避免 shared 包直接依赖前端框架工具库。
 */
export interface CreateWebRequestOptions extends CreateRequestOptions {
  /** 请求开始时的回调（如 nprogress.start） */
  onProgressStart?: () => void
  /** 请求结束（成功或失败）时的回调（如 nprogress.done） */
  onProgressDone?: () => void
  /** 非 401 错误时的回调（如 message.error） */
  onError?: (msg: string) => void
}

export function createWebRequest(opts: CreateWebRequestOptions): RequestInstance {
  const { onProgressStart, onProgressDone, onError, ...rest } = opts
  const instance = createRequest(rest)

  instance.interceptors.request.use((config) => {
    onProgressStart?.()
    return config
  })

  instance.interceptors.response.use(
    (resp) => {
      onProgressDone?.()
      return resp
    },
    (error) => {
      onProgressDone?.()
      if (error.response?.status !== 401) {
        const abpError = error.response?.data?.error
        const msg = abpError?.message || error.message || '请求失败'
        onError?.(msg)
      }
      return Promise.reject(error)
    },
  )

  return instance
}
