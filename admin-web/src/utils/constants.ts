// 仅在开发模式启用 mock，生产构建自动关闭，确保真实 API 被调用
// 如需在 dev 模式下调试真实 API，在 .env.local 中设置 VITE_USE_MOCK=false
export const USE_MOCK = import.meta.env.DEV && import.meta.env.VITE_USE_MOCK !== 'false'

/** 按模块控制 mock：设为 false 则该模块请求直连真实 API */
export const MOCK_MODULES: Record<string, boolean> = {
  dashboard: true,
  permissions: true,
  applications: true,
  datasource: true,
  analytics: true,
  profile: true,
  apikey: true,
  dubbing: true,
}

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api/admin/'
export const STORAGE_TOKEN_KEY = 'DREDGE_AI_ADMIN_TOKEN'

export const USER_WEB_URL = import.meta.env.VITE_USER_WEB_URL || 'http://localhost:5373'

export const PAGE_SIZE = 20

export const MENU_GROUP_MAIN = 'main'
export const MENU_GROUP_ACCOUNT = 'account'
