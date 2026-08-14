import { createRequest } from '@shared/core/http'
import type { AxiosRequestConfig } from 'axios'
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

function isSilentRequest(config?: AxiosRequestConfig): boolean {
  const headers = config?.headers as Record<string, unknown> | undefined
  return headers?.['X-Silent-Request'] === '1' || headers?.['x-silent-request'] === '1'
}

export function createWebRequest(opts: CreateWebRequestOptions): RequestInstance {
  const { onProgressStart, onProgressDone, onError, ...rest } = opts
  const instance = createRequest(rest)

  function track<T>(promise: Promise<T>, silent: boolean): Promise<T> {
    if (!silent) onProgressStart?.()
    return promise.then(
      (data) => {
        if (!silent) onProgressDone?.()
        return data
      },
      (error) => {
        if (!silent) {
          onProgressDone?.()
          if (error.response?.status !== 401) {
            const abpError = error.response?.data?.error
            const msg = abpError?.message || error.message || '请求失败'
            onError?.(msg)
          }
        }
        return Promise.reject(error)
      },
    )
  }

  return {
    get: <T = unknown>(url: string, config?: AxiosRequestConfig) =>
      track(instance.get<T>(url, config), !isSilentRequest(config)),
    post: <T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
      track(instance.post<T>(url, data, config), !isSilentRequest(config)),
    put: <T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
      track(instance.put<T>(url, data, config), !isSilentRequest(config)),
    patch: <T = unknown>(url: string, data?: unknown, config?: AxiosRequestConfig) =>
      track(instance.patch<T>(url, data, config), !isSilentRequest(config)),
    delete: <T = unknown>(url: string, config?: AxiosRequestConfig) =>
      track(instance.delete<T>(url, config), !isSilentRequest(config)),
    interceptors: instance.interceptors,
    raw: instance.raw,
  }
}
