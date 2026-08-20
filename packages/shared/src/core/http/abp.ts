import type { AxiosInstance, AxiosResponse } from 'axios'
import type { AbpErrorInfo } from '../types/abp'

/** ABP 业务错误：保留 code / data / details，供调用方针对具体错误给出提示 */
export class AbpError extends Error {
  readonly code: string | null
  readonly data: Record<string, unknown> | null
  readonly details: string | null
  readonly status: number | undefined

  constructor(info: AbpErrorInfo, status?: number) {
    super(info.message || '请求失败')
    this.name = 'AbpError'
    this.code = info.code
    this.data = info.data
    this.details = info.details
    this.status = status
  }
}

/** 给 axios 实例挂载 ABP 协议响应/错误拦截器 */
export function applyAbpInterceptors(
  instance: AxiosInstance,
  opts: { onUnauthorized?: () => void } = {},
): void {
  const { onUnauthorized } = opts

  instance.interceptors.response.use(
    (response: AxiosResponse) => {
      // ABP 格式：成功响应直接返回数据体
      return response.data as unknown as AxiosResponse
    },
    (error) => {
      const status = error.response?.status
      if (status === 401 && onUnauthorized) onUnauthorized()

      const abpError: AbpErrorInfo | undefined = error.response?.data?.error
      if (abpError) {
        return Promise.reject(new AbpError(abpError, status))
      }
      return Promise.reject(error)
    },
  )
}
