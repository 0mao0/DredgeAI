import { urls } from '@shared/core/api/urls'
import type { TokenResponse } from '@shared/core/types/auth'
import { AUTH_CLIENT_ID, AUTH_SCOPE } from '@/utils/constants'

/**
 * Auth 认证中心（OpenIddict /connect/*）API。
 * 端点不在 API baseURL（/api/）之下，故用原生 fetch 拼同源路径
 * （dev 由 vite /connect 代理转发到 Auth 服务），不经过 request 实例 / mock。
 */

async function postTokenForm(body: URLSearchParams): Promise<TokenResponse> {
  const res = await fetch(urls.authToken, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body,
  })
  if (!res.ok) {
    let reason = ''
    try {
      const data = (await res.json()) as { error?: string, error_description?: string }
      reason = data.error_description || data.error || ''
    } catch {
      // 非 JSON 错误体（网关 5xx 等），无结构可解析
    }
    throw new Error(reason || `认证服务请求失败（HTTP ${res.status}）`)
  }
  return (await res.json()) as TokenResponse
}

/** 账号密码登录（password grant） */
export function loginWithPassword(username: string, password: string): Promise<TokenResponse> {
  return postTokenForm(
    new URLSearchParams({
      grant_type: 'password',
      username,
      password,
      client_id: AUTH_CLIENT_ID,
      scope: AUTH_SCOPE,
    }),
  )
}

/** 用 refresh_token 换新 token（自动续期） */
export function loginWithRefreshToken(refreshToken: string): Promise<TokenResponse> {
  return postTokenForm(
    new URLSearchParams({
      grant_type: 'refresh_token',
      refresh_token: refreshToken,
      client_id: AUTH_CLIENT_ID,
    }),
  )
}

/** OIDC 授权码回调落地：code + PKCE verifier 换 token */
export function exchangeAuthCode(
  code: string,
  redirectUri: string,
  codeVerifier: string,
): Promise<TokenResponse> {
  return postTokenForm(
    new URLSearchParams({
      grant_type: 'authorization_code',
      code,
      redirect_uri: redirectUri,
      client_id: AUTH_CLIENT_ID,
      code_verifier: codeVerifier,
    }),
  )
}

/** 拼接 /connect/authorize 跳转地址（同源相对路径，浏览器整页跳转） */
export function buildAuthorizeUrl(redirectUri: string, state: string, codeChallenge: string): string {
  const params = new URLSearchParams({
    response_type: 'code',
    client_id: AUTH_CLIENT_ID,
    redirect_uri: redirectUri,
    scope: AUTH_SCOPE,
    state,
    code_challenge: codeChallenge,
    code_challenge_method: 'S256',
  })
  return `${urls.authAuthorize}?${params.toString()}`
}
