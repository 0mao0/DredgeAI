import { messages } from './messages'
import type { Locale } from './messages'

/** 默认语言 */
const DEFAULT_LOCALE: Locale = 'zh-CN'

/** 当前生效语言（模块级单例，跨端共用） */
let currentLocale: Locale = DEFAULT_LOCALE

/** 读取当前语言 */
export function getLocale(): Locale {
  return currentLocale
}

/** 设置当前语言 */
export function setLocale(locale: Locale): void {
  currentLocale = locale
}

/** 按 key 翻译，未命中时返回 key 自身 */
export function t(key: string, params?: Record<string, string | number>): string {
  const table = messages[currentLocale] ?? messages[DEFAULT_LOCALE]
  let text = table[key] ?? key
  if (params) {
    for (const [k, v] of Object.entries(params)) {
      text = text.replace(new RegExp(`\\{${k}\\}`, 'g'), String(v))
    }
  }
  return text
}
