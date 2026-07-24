import nprogress from 'nprogress'
import 'nprogress/nprogress.css'
import { message } from 'ant-design-vue'
import { createWebRequest } from '@shared/web/http/createWebRequest'
import { API_BASE_URL, STORAGE_TOKEN_KEY } from '@/utils/constants'

/** admin-web 专属 request 实例：注入 nprogress + 全局错误 toast + 未授权处理 */
const instance = createWebRequest({
  baseURL: API_BASE_URL,
  tokenKey: STORAGE_TOKEN_KEY,
  onUnauthorized: () => {
    localStorage.removeItem(STORAGE_TOKEN_KEY)
  },
  onProgressStart: () => nprogress.start(),
  onProgressDone: () => nprogress.done(),
  onError: (msg) => message.error(msg),
})

export default instance

// 兼容旧导出
export type { AbpErrorInfo, AbpErrorResponse, PagedResult } from '@shared/core/types'
