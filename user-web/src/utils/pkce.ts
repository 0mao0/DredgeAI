/**
 * PKCE（RFC 7636）工具：供 OIDC authorization_code + PKCE 授权码流程使用。
 * crypto.subtle 仅在安全上下文可用（localhost / https，均满足）。
 */

function base64UrlEncode(bytes: Uint8Array): string {
  let binary = ''
  for (const b of bytes) binary += String.fromCharCode(b)
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

/** 64 字节随机 code_verifier（base64url 后 86 字符，符合 RFC 7636 43–128 字符要求） */
export function createCodeVerifier(): string {
  const bytes = new Uint8Array(64)
  crypto.getRandomValues(bytes)
  return base64UrlEncode(bytes)
}

/** S256 code_challenge = BASE64URL(SHA-256(code_verifier)) */
export async function createCodeChallenge(verifier: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier))
  return base64UrlEncode(new Uint8Array(digest))
}

/** 16 字节随机 state（CSRF 防护，回调时与 sessionStorage 中的值比对） */
export function createState(): string {
  const bytes = new Uint8Array(16)
  crypto.getRandomValues(bytes)
  return base64UrlEncode(bytes)
}
