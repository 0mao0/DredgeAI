/** 支持的语言列表 */
export type Locale = 'zh-CN' | 'en-US'

/** 简易消息表类型 */
export type Messages = Record<string, string>

/** zh-CN 默认消息表（占位骨架，后续按页面逐步补充） */
export const zhCN: Messages = {
  'app.title.user': '智浚 AI · 用户端',
  'app.title.admin': '智浚 AI · 管理后台',
  'common.loading': '加载中…',
  'common.empty': '暂无数据',
  'common.error': '出现异常',
  'common.retry': '重试',
  'common.reload': '刷新页面',
}

/** en-US 消息表（占位骨架） */
export const enUS: Messages = {
  'app.title.user': 'DredgeAI · User',
  'app.title.admin': 'DredgeAI · Admin',
  'common.loading': 'Loading…',
  'common.empty': 'No data',
  'common.error': 'Something went wrong',
  'common.retry': 'Retry',
  'common.reload': 'Reload',
}

/** 各语言消息表索引 */
export const messages: Record<Locale, Messages> = {
  'zh-CN': zhCN,
  'en-US': enUS,
}
