/** OpenIddict /connect/token 响应 */
export interface TokenResponse {
  access_token: string
  refresh_token?: string
  expires_in: number // 秒
  token_type: string // 'Bearer'
  scope?: string
}
