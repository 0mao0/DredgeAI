import MockAdapter from 'axios-mock-adapter'
import request from '@/api/request'
import { USE_MOCK } from '@/utils/constants'
import { registerDashboardMock } from './routes/dashboard'
import { registerPermissionMock } from './routes/permissions'
import { registerApplicationMock } from './routes/applications'
import { registerDatasourceMock } from './routes/datasource'
import { registerAnalyticsMock } from './routes/analytics'
import { registerProfileMock } from './routes/profile'
import { registerApiKeyMock } from './routes/apikey'
import { registerDubbingMock } from './routes/dubbing'

/** 注册所有 mock 路由 */
export function registerMock(): void {
  console.log('[mock] registerMock called, USE_MOCK =', USE_MOCK)
  if (!USE_MOCK) return

  const mock = new MockAdapter(request, { delayResponse: 0 })

  // ABP 格式：成功响应直接返回数据体，不包裹 { code, data, message }
  const wrap = (handler: () => unknown) => async (config?: { method?: string; url?: string }): Promise<[number, unknown]> => {
    console.log('[mock] hit:', config?.method?.toUpperCase(), config?.url)
    return [200, handler()]
  }

  registerDashboardMock(mock, wrap)
  registerPermissionMock(mock, wrap)
  registerApplicationMock(mock, wrap)
  registerDatasourceMock(mock, wrap)
  registerAnalyticsMock(mock, wrap)
  registerProfileMock(mock, wrap)
  registerApiKeyMock(mock, wrap)
  registerDubbingMock(mock, wrap)

  // 打印已注册的处理器数量（用于诊断）
  console.log('[mock] routes registered')

  // ABP 格式：未匹配的请求返回错误响应
  mock.onAny().reply((config) => {
    console.warn('[mock] NO MATCH for:', config.method?.toUpperCase(), config.url, 'baseURL:', config.baseURL)
    return [404, {
      error: {
        code: null,
        message: `Mock not found: ${config.method?.toUpperCase()} ${config.url}`,
        details: null,
        data: null,
        validationErrors: null,
      },
    }]
  })
}