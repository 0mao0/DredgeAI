import type { AxiosInstance, AxiosResponse } from 'axios'
import type { AbpErrorInfo } from '../types/abp'

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
        return Promise.reject(new Error(abpError.message || '请求失败'))
      }
      return Promise.reject(error)
    },
  )
}
