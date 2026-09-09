export const STORAGE_TOKEN_KEY = 'DREDGE_AI_TOKEN'

// 登录态 cookie（唯一持久层；STORAGE_TOKEN_KEY 同时作为 access token 的 cookie 名）。
// 命名带 DREDGE_AI_ 前缀与 admin-web 的 DREDGE_AI_ADMIN_* 区分：cookie 按主机共享、端口不隔离
export const REFRESH_TOKEN_COOKIE = 'DREDGE_AI_REFRESH_TOKEN'
/** access token 过期时刻（epoch 毫秒），供自动刷新调度使用 */
export const TOKEN_EXPIRES_AT_COOKIE = 'DREDGE_AI_TOKEN_EXPIRES_AT'
/** access cookie 有效期 = token 有效期 − 5 分钟（临过期刷新窗口） */
export const ACCESS_TOKEN_MAX_AGE_OFFSET_SEC = 300
export const REFRESH_TOKEN_COOKIE_MAX_AGE_SEC = 14 * 24 * 3600

// OIDC / OpenIddict 参数
export const AUTH_CLIENT_ID = 'DredgeAI_App'
/** offline_access 才会下发 refresh_token（ABP/OpenIddict 约定） */
export const AUTH_SCOPE = 'DredgeAI offline_access'
export const AUTH_CALLBACK_PATH = '/auth/callback'
export const LOGIN_PATH = '/login'

export const ADMIN_WEB_URL = import.meta.env.VITE_ADMIN_WEB_URL || 'http://localhost:5374'

export const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api/'

// 仅在开发模式启用 mock，生产构建自动关闭，确保真实 API 被调用
// 如需在 dev 模式下调试真实 API，在 .env.local 中设置 VITE_USE_MOCK=false
export const USE_MOCK = import.meta.env.DEV && import.meta.env.VITE_USE_MOCK !== 'false'

/** 按模块控制 mock：设为 false 则该模块请求直连真实 API */
export const MOCK_MODULES: Record<string, boolean> = {
  user: true,
  // 应用列表直连真实后端（与 admin 发布管理共享同一份目录）
  app: false,
  task: true,
  file: true,
  bid: true,
  standard: true,
  apikey: true,
  chart: true,
  dubbing: true,
  meeting: false, // AI 晨会已接入真实后端（meeting-bot + ABP），关闭 mock
  // compare 直连真实后端（上传会话依赖后端存储），如需 mock 设为 true 并提供注册器
  compare: false,
  // tenderRead 直连真实后端（解析/提取由后端 AnGIneer 管线执行）
  tenderRead: false,
  // 应用顺序直连真实后端（admin / user-web 共享同一后端进程）
  appOrder: false,
}

// TTS Mock 开关：关闭后 TTS 请求直连 CosyVoice server.py（http://localhost:8000）
export const USE_TTS_MOCK = false
