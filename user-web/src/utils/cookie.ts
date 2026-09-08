/**
 * 轻量 cookie 工具：user-web 登录态以 cookie 为唯一持久层
 * （access_token / refresh_token / 过期时刻，见 @/utils/constants）。
 */

/** 读取 cookie；不存在返回 null */
export function getCookie(name: string): string | null {
  const prefix = `${name}=`
  const parts = document.cookie.split(';')
  for (const part of parts) {
    const item = part.trim()
    if (item.startsWith(prefix)) {
      return decodeURIComponent(item.slice(prefix.length))
    }
  }
  return null
}

/** 写入 cookie：path=/、SameSite=Lax；dev 为 http，不加 Secure */
export function setCookie(name: string, value: string, maxAgeSec: number): void {
  document.cookie = `${name}=${encodeURIComponent(value)}; max-age=${Math.floor(maxAgeSec)}; path=/; SameSite=Lax`
}

/** 删除 cookie（max-age=0 立即过期，其余属性与写入时一致） */
export function removeCookie(name: string): void {
  document.cookie = `${name}=; max-age=0; path=/; SameSite=Lax`
}
