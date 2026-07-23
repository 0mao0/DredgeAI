// 仅在开发模式启用 mock，生产构建自动关闭，确保真实 API 被调用
export const USE_MOCK = import.meta.env.DEV

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

export const API_BASE_URL = '/api/admin'
export const STORAGE_TOKEN_KEY = 'DREDGE_AI_ADMIN_TOKEN'

export const USER_WEB_URL = 'http://localhost:5373'

export const PAGE_SIZE = 20

export const MENU_GROUP_MAIN = 'main'
export const MENU_GROUP_ACCOUNT = 'account'
