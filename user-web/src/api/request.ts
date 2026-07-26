import { createNprogressRequest } from '@shared/web/http/createNprogressRequest'
import { API_BASE_URL, STORAGE_TOKEN_KEY } from '@/utils/constants'

/** user-web 专属 request 实例：nprogress + 全局错误 toast + 未授权处理 */
const instance = createNprogressRequest({
  baseURL: API_BASE_URL,
  tokenKey: STORAGE_TOKEN_KEY,
  onUnauthorized: () => {
    // 路由守卫层处理跳转，这里仅清理 token
    localStorage.removeItem(STORAGE_TOKEN_KEY)
  },
})

export default instance

// 兼容旧导出
export type { AbpErrorInfo, AbpErrorResponse, PagedResult } from '@shared/core/types'
