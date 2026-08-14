import MockAdapter from 'axios-mock-adapter'
import axios from 'axios'
import type { AxiosHeaders, AxiosRequestConfig } from 'axios'
import request from '@/api/request'
import { ttsClient } from '@/api/modules/dubbing'
import { API_BASE_URL, USE_MOCK, USE_TTS_MOCK, MOCK_MODULES } from '@/utils/constants'
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

  // 模块 mock 关闭时转发到真实 API（保留 baseURL，避免 mock 库 passThrough 丢 /api 前缀）
  const forwardToRealApi = async (config: AxiosRequestConfig): Promise<[number, unknown]> => {
    const headers: Record<string, string> = {}
    const rawHeaders = config.headers as AxiosHeaders | undefined
    if (rawHeaders && typeof rawHeaders.toJSON === 'function') {
      Object.assign(headers, rawHeaders.toJSON())
    }

    // FormData 的 Content-Type 由浏览器自动生成（含 boundary），显式转发会破坏上传
    if (typeof FormData !== 'undefined' && config.data instanceof FormData) {
      delete headers['Content-Type']
    }

    try {
      const res = await axios.request<unknown>({
        method: config.method,
        url: config.url,
        baseURL: API_BASE_URL,
        data: config.data,
        params: config.params,
        headers,
        responseType: config.responseType,
        timeout: config.timeout,
        onUploadProgress: config.onUploadProgress,
        onDownloadProgress: config.onDownloadProgress,
      })
      return [res.status, res.data]
    } catch (error) {
      if (axios.isAxiosError(error) && error.response) {
        return [error.response.status, error.response.data]
      }
      throw error
    }
  }

  // 按模块注册 mock，模块开关关闭则该模块请求直连真实 API
  const modules: { key: string, register: (m: MockAdapter, w: typeof wrap) => void, passthrough?: RegExp }[] = [
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
    if (MOCK_MODULES[mod.key] === false) {
      // 模块 mock 关闭时直连真实 API（Vite 代理在 vite.config.ts 中配置）
      if (mod.passthrough) mock.onAny(mod.passthrough).reply(forwardToRealApi)
      continue
    }
    mod.register(mock, wrap)
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
