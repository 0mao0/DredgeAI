export const STORAGE_TOKEN_KEY = 'DREDGE_AI_TOKEN'

export const ADMIN_WEB_URL = 'http://localhost:5374'

export const API_BASE_URL = '/api'

// 仅在开发模式启用 mock，生产构建自动关闭，确保真实 API 被调用
export const USE_MOCK = import.meta.env.DEV

/** 按模块控制 mock：设为 false 则该模块请求直连真实 API */
export const MOCK_MODULES: Record<string, boolean> = {
  user: true,
  app: true,
  task: true,
  file: true,
  bid: true,
  standard: true,
  apikey: true,
  chart: true,
  dubbing: true,
}

// TTS Mock 开关：关闭后 TTS 请求直连 CosyVoice server.py（http://localhost:8000）
export const USE_TTS_MOCK = false
