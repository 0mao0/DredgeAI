import MockAdapter from 'axios-mock-adapter'
import request from '@/api/request'
import { USE_MOCK } from '@/utils/constants'
import { registerUserMock } from './routes/user'
import { registerAppMock } from './routes/app'
import { registerTaskMock } from './routes/task'
import { registerFileMock } from './routes/file'
import { registerBidMock } from './routes/bid'
import { registerStandardMock } from './routes/standard'
import { registerApiKeyMock } from './routes/apikey'
import { registerChartMock } from './routes/chart'

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

  registerUserMock(mock, wrap)
  registerAppMock(mock, wrap)
  registerTaskMock(mock, wrap)
  registerFileMock(mock, wrap)
  registerBidMock(mock, wrap)
  registerStandardMock(mock, wrap)
  registerApiKeyMock(mock, wrap)
  registerChartMock(mock, wrap)

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