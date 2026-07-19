import nprogress from 'nprogress'
import 'nprogress/nprogress.css'
import { createRequest } from '@shared/core/http'
import { API_BASE_URL, STORAGE_TOKEN_KEY } from '@/utils/constants'

/** user-web 专属 request 实例：注入 nprogress 与未授权处理 */
const instance = createRequest({
  baseURL: API_BASE_URL,
  tokenKey: STORAGE_TOKEN_KEY,
  onUnauthorized: () => {
    // 路由守卫层处理跳转，这里仅清理 token
    localStorage.removeItem(STORAGE_TOKEN_KEY)
  },
})

// 注入 nprogress 进度条（web 端专属，core 不感知）
instance.interceptors.request.use((config) => {
  nprogress.start()
  return config
})
instance.interceptors.response.use(
  (resp) => { nprogress.done(); return resp },
  (err) => { nprogress.done(); return Promise.reject(err) },
)

export default instance

// 兼容旧导出
export type { AbpErrorInfo, AbpErrorResponse, PagedResult } from '@shared/core/types'
export function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms))
}
export function randomDelay(): Promise<void> {
  return delay(200 + Math.floor(Math.random() * 200))
}
