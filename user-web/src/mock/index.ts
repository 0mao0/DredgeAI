import MockAdapter from 'axios-mock-adapter'
import request from '@/api/request'
import { ttsClient } from '@/api/modules/dubbing'
import { USE_MOCK, USE_TTS_MOCK, MOCK_MODULES } from '@/utils/constants'
import { registerUserMock } from './routes/user'
import { registerAppMock } from './routes/app'
import { registerTaskMock } from './routes/task'
import { registerFileMock } from './routes/file'
import { registerBidMock } from './routes/bid'
import { registerStandardMock } from './routes/standard'
import { registerApiKeyMock } from './routes/apikey'
import { registerChartMock } from './routes/chart'
import { registerDubbingMock } from './routes/dubbing'
import { registerDubbingTtsMock } from './routes/dubbing-tts'

/** 注册所有 mock 路由（按模块开关控制） */
export function registerMock(): void {
  if (!USE_MOCK) return

  const mock = new MockAdapter(request.raw, { delayResponse: 0 })

  // ABP 格式：成功响应直接返回数据体，不包裹 { code, data, message }
  const wrap = (handler: () => unknown) => async (): Promise<[number, unknown]> => {
    return [200, handler()]
  }

  // 按模块注册 mock，模块开关关闭则该模块请求直连真实 API
  const modules: { key: string, register: (m: MockAdapter, w: typeof wrap) => void }[] = [
    { key: 'user', register: registerUserMock },
    { key: 'app', register: registerAppMock },
    { key: 'task', register: registerTaskMock },
    { key: 'file', register: registerFileMock },
    { key: 'bid', register: registerBidMock },
    { key: 'standard', register: registerStandardMock },
    { key: 'apikey', register: registerApiKeyMock },
    { key: 'chart', register: registerChartMock },
    { key: 'dubbing', register: registerDubbingMock },
  ]

  for (const mod of modules) {
    if (MOCK_MODULES[mod.key] !== false) {
      mod.register(mock, wrap)
    }
  }

  // ABP 格式：未匹配的请求返回错误响应
  mock.onAny().reply((config) => {
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

  // TTS 服务 mock（ttsClient 独立实例，需单独挂 MockAdapter）
  // 仅当 USE_TTS_MOCK 为 true 时启用，否则直连 server.py:8000
  if (USE_TTS_MOCK) {
    const ttsMock = new MockAdapter(ttsClient, { delayResponse: 0 })
    registerDubbingTtsMock(ttsMock)
    // 文本合成走真实服务，mock 不拦截
    ttsMock.onPost('/tts/tts').passThrough()
    ttsMock.onAny().reply((config) => {
      return [404, { message: `TTS Mock not found: ${config.method?.toUpperCase()} ${config.url}` }]
    })
  }
}
