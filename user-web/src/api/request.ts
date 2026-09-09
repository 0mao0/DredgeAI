import { createNprogressRequest } from '@shared/web/http/createNprogressRequest'
import { getCookie, removeCookie } from '@/utils/cookie'
import {
  API_BASE_URL,
  LOGIN_PATH,
  REFRESH_TOKEN_COOKIE,
  STORAGE_TOKEN_KEY,
  TOKEN_EXPIRES_AT_COOKIE,
} from '@/utils/constants'

/**
 * user-web 专属 request 实例：nprogress + 全局错误 toast + 未授权处理。
 * token 源为 cookie（登录页写入的唯一持久层）；tokenKey/localStorage 仅作兜底。
 */
const instance = createNprogressRequest({
  baseURL: API_BASE_URL,
  tokenKey: STORAGE_TOKEN_KEY,
  getToken: () => getCookie(STORAGE_TOKEN_KEY),
  onUnauthorized: () => {
    // 直接清 cookie（不经 auth store：避免 request ↔ store 循环依赖）
    removeCookie(STORAGE_TOKEN_KEY)
    removeCookie(REFRESH_TOKEN_COOKIE)
    removeCookie(TOKEN_EXPIRES_AT_COOKIE)
    // 不在登录页时整页跳登录，携带原地址供登录成功后跳回
    if (!window.location.pathname.startsWith(LOGIN_PATH)) {
      const redirect = encodeURIComponent(window.location.pathname + window.location.search)
      window.location.href = `${LOGIN_PATH}?redirect=${redirect}`
    }
  },
})

export default instance

// 兼容旧导出
export type { AbpErrorInfo, AbpErrorResponse, PagedResult } from '@shared/core/types'
