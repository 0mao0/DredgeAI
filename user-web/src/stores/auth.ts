import { defineStore } from 'pinia'
import type { TokenResponse } from '@shared/core/types/auth'
import {
  buildAuthorizeUrl,
  exchangeAuthCode,
  loginWithPassword,
  loginWithRefreshToken,
} from '@/api/modules/auth'
import { getCookie, removeCookie, setCookie } from '@/utils/cookie'
import { createCodeChallenge, createCodeVerifier, createState } from '@/utils/pkce'
import {
  ACCESS_TOKEN_MAX_AGE_OFFSET_SEC,
  AUTH_CALLBACK_PATH,
  LOGIN_PATH,
  REFRESH_TOKEN_COOKIE,
  REFRESH_TOKEN_COOKIE_MAX_AGE_SEC,
  STORAGE_TOKEN_KEY,
  TOKEN_EXPIRES_AT_COOKIE,
} from '@/utils/constants'

/** OIDC 授权码流程中间态 key（sessionStorage；AuthCallback 页校验 state 时读同一常量） */
export const OAUTH_STATE_KEY = 'DREDGE_AI_OAUTH_STATE'
export const PKCE_VERIFIER_KEY = 'DREDGE_AI_PKCE_VERIFIER'

/** 模块级刷新定时器（cookie 之外的运行态全部收敛于此，store 仅做内存编排） */
let refreshTimer: ReturnType<typeof setTimeout> | null = null
/** 进行中的刷新请求：单飞，并发调用（守卫/定时器/init）共享同一请求 */
let refreshPromise: Promise<boolean> | null = null

/** 从 cookie 读取 access token 过期时刻（epoch 毫秒） */
function readExpiresAtMs(): number | null {
  const raw = getCookie(TOKEN_EXPIRES_AT_COOKIE)
  if (!raw) return null
  const ms = Number(raw)
  return Number.isFinite(ms) && ms > 0 ? ms : null
}

export const useAuthStore = defineStore('auth', () => {
  /** 写入三枚 token cookie；access cookie 有效期比 token 短 5 分钟，留出刷新窗口 */
  function saveTokens(t: TokenResponse): void {
    const expiresAt = Date.now() + t.expires_in * 1000
    setCookie(
      STORAGE_TOKEN_KEY,
      t.access_token,
      Math.max(t.expires_in - ACCESS_TOKEN_MAX_AGE_OFFSET_SEC, 30),
    )
    // 过期时刻 cookie 多活 5 分钟：access cookie 到期后、token 真正过期前的窗口内仍可读出
    setCookie(TOKEN_EXPIRES_AT_COOKIE, String(expiresAt), t.expires_in)
    if (t.refresh_token) {
      setCookie(REFRESH_TOKEN_COOKIE, t.refresh_token, REFRESH_TOKEN_COOKIE_MAX_AGE_SEC)
    }
  }

  /** 清除全部登录态 cookie 并停掉刷新定时器 */
  function clearTokens(): void {
    if (refreshTimer) {
      clearTimeout(refreshTimer)
      refreshTimer = null
    }
    removeCookie(STORAGE_TOKEN_KEY)
    removeCookie(TOKEN_EXPIRES_AT_COOKIE)
    removeCookie(REFRESH_TOKEN_COOKIE)
  }

  /** 在 token 到期前 5 分钟触发一次刷新；此后由 refresh 成功路径续排 */
  function scheduleRefresh(): void {
    if (refreshTimer) {
      clearTimeout(refreshTimer)
      refreshTimer = null
    }
    // 未下发 refresh_token（无续期能力）时不调度：access cookie 到期自然失效，由 401 兜底
    if (!getCookie(REFRESH_TOKEN_COOKIE)) return
    const expiresAt = readExpiresAtMs()
    const delay
      = expiresAt === null
        ? ACCESS_TOKEN_MAX_AGE_OFFSET_SEC * 1000
        : expiresAt - Date.now() - ACCESS_TOKEN_MAX_AGE_OFFSET_SEC * 1000
    refreshTimer = setTimeout(() => {
      void refresh()
    }, Math.max(delay, 10_000))
  }

  /** 用 refresh_token 静默续期（单飞）；失败即清空登录态（由守卫引导回登录页） */
  function refresh(): Promise<boolean> {
    if (refreshPromise) return refreshPromise
    refreshPromise = performRefresh().finally(() => {
      refreshPromise = null
    })
    return refreshPromise
  }

  async function performRefresh(): Promise<boolean> {
    const refreshToken = getCookie(REFRESH_TOKEN_COOKIE)
    if (!refreshToken) {
      clearTokens()
      return false
    }
    try {
      const t = await loginWithRefreshToken(refreshToken)
      saveTokens(t)
      scheduleRefresh()
      return true
    } catch {
      clearTokens()
      return false
    }
  }

  /** 应用启动恢复登录态（App.vue onMounted 调用，幂等） */
  function init(): void {
    if (refreshTimer !== null || refreshPromise !== null) return // 已在调度 / 刷新中
    const refreshToken = getCookie(REFRESH_TOKEN_COOKIE)
    if (!refreshToken) return // 未登录（残留 access cookie 由 401 处理跳回登录页）
    const accessToken = getCookie(STORAGE_TOKEN_KEY)
    const expiresAt = readExpiresAtMs()
    const remaining = expiresAt === null ? 0 : expiresAt - Date.now()
    if (!accessToken || remaining < ACCESS_TOKEN_MAX_AGE_OFFSET_SEC * 1000) {
      void refresh()
    } else {
      scheduleRefresh()
    }
  }

  /** 账号密码登录：成功后写入 cookie 并调度自动刷新 */
  async function login(username: string, password: string): Promise<void> {
    const t = await loginWithPassword(username, password)
    saveTokens(t)
    scheduleRefresh()
  }

  /** OIDC 回调落地：校验通过的 code 换 token；中间态用完即清除 */
  async function completeOidc(code: string): Promise<void> {
    const verifier = sessionStorage.getItem(PKCE_VERIFIER_KEY)
    sessionStorage.removeItem(PKCE_VERIFIER_KEY)
    sessionStorage.removeItem(OAUTH_STATE_KEY)
    if (!verifier) throw new Error('登录状态缺失，请重新发起登录')
    const t = await exchangeAuthCode(code, `${window.location.origin}${AUTH_CALLBACK_PATH}`, verifier)
    saveTokens(t)
    scheduleRefresh()
  }

  /** 跳转 Auth 认证中心：state + PKCE 中间态先入 sessionStorage */
  function startAuthCenterLogin(): void {
    const state = createState()
    const verifier = createCodeVerifier()
    sessionStorage.setItem(OAUTH_STATE_KEY, state)
    sessionStorage.setItem(PKCE_VERIFIER_KEY, verifier)
    const redirectUri = `${window.location.origin}${AUTH_CALLBACK_PATH}`
    void createCodeChallenge(verifier).then((challenge) => {
      window.location.href = buildAuthorizeUrl(redirectUri, state, challenge)
    })
  }

  /** 退出登录：清 cookie + 回登录页 */
  function logout(): void {
    clearTokens()
    window.location.href = LOGIN_PATH
  }

  /**
   * 供路由前置守卫调用：进入受保护页前，若 access token 缺失或临过期（且存在
   * refresh token）则立即静默刷新，避免页面数据请求以旧 token 撞 401。
   */
  async function ensureFreshToken(): Promise<void> {
    const accessToken = getCookie(STORAGE_TOKEN_KEY)
    const refreshToken = getCookie(REFRESH_TOKEN_COOKIE)
    if (!refreshToken) return
    const expiresAt = readExpiresAtMs()
    const remaining = expiresAt === null ? 0 : expiresAt - Date.now()
    if (accessToken && remaining >= ACCESS_TOKEN_MAX_AGE_OFFSET_SEC * 1000) return
    await refresh()
  }

  return {
    saveTokens,
    clearTokens,
    scheduleRefresh,
    refresh,
    ensureFreshToken,
    init,
    login,
    completeOidc,
    startAuthCenterLogin,
    logout,
  }
})
