// 仅在开发模式启用 mock，生产构建自动关闭，确保真实 API 被调用
// 如需在 dev 模式下调试真实 API，在 .env.local 中设置 VITE_USE_MOCK=false
export const USE_MOCK = import.meta.env.DEV && import.meta.env.VITE_USE_MOCK !== 'false'

/** 按模块控制 mock：设为 false 则该模块请求直连真实 API */
export const MOCK_MODULES: Record<string, boolean> = {
  dashboard: true,
  // 应用配置直连真实后端（权限码唯一来源：ABP grantedPolicies）
  appConfig: false,
  // 应用目录直连真实后端（admin 发布管理 / user-web 应用列表共享同一份数据）
  applications: false,
  datasource: true,
  analytics: true,
  profile: false,
  apikey: true,
  dubbing: true,
  // 组织用户直连真实后端（/base/users + /base/identity/roles）
  orgUsers: false,
  roles: true,
  orgUnits: false,
  standards: true,
  // 应用顺序直连真实后端（admin / user-web 共享同一后端进程）
  appOrder: false,
}

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api/'

// 登录态 cookie（唯一持久层；STORAGE_TOKEN_KEY 同时作为 access token 的 cookie 名）
export const STORAGE_TOKEN_KEY = 'DREDGE_AI_ADMIN_TOKEN'
export const REFRESH_TOKEN_COOKIE = 'DREDGE_AI_ADMIN_REFRESH_TOKEN'
/** access token 过期时刻（epoch 毫秒），供自动刷新调度使用 */
export const TOKEN_EXPIRES_AT_COOKIE = 'DREDGE_AI_ADMIN_TOKEN_EXPIRES_AT'
/** access cookie 有效期 = token 有效期 − 5 分钟（临过期刷新窗口） */
export const ACCESS_TOKEN_MAX_AGE_OFFSET_SEC = 300
export const REFRESH_TOKEN_COOKIE_MAX_AGE_SEC = 14 * 24 * 3600

// OIDC / OpenIddict 参数
export const AUTH_CLIENT_ID = 'DredgeAI_App'
/** offline_access 才会下发 refresh_token（ABP/OpenIddict 约定） */
export const AUTH_SCOPE = 'DredgeAI offline_access'
export const AUTH_CALLBACK_PATH = '/auth/callback'
export const LOGIN_PATH = '/login'

export const USER_WEB_URL = import.meta.env.VITE_USER_WEB_URL || 'http://localhost:5373'

export const MENU_GROUP_MAIN = 'main'
export const MENU_GROUP_ACCOUNT = 'account'
