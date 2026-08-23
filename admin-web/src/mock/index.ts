import MockAdapter from 'axios-mock-adapter'
import axios from 'axios'
import type { AxiosHeaders, AxiosRequestConfig } from 'axios'
import request from '@/api/request'
import { API_BASE_URL, USE_MOCK, MOCK_MODULES } from '@/utils/constants'
import { registerDashboardMock } from './routes/dashboard'
import { registerPermissionMock } from './routes/permissions'
import { registerApplicationMock } from './routes/applications'
import { registerDatasourceMock } from './routes/datasource'
import { registerAnalyticsMock } from './routes/analytics'
import { registerProfileMock } from './routes/profile'
import { registerApiKeyMock } from './routes/apikey'
import { registerDubbingMock } from './routes/dubbing'
import { registerOrgUsersMock } from './routes/org-users'
import { registerRolesMock } from './routes/roles'
import { registerStandardsMock } from './routes/standards'

/** 注册所有 mock 路由（按模块开关控制） */
export function registerMock(): void {
  if (!USE_MOCK) return

  const mock = new MockAdapter(request.raw, { delayResponse: 0 })

  // ABP 格式：成功响应直接返回数据体，不包裹 { code, data, message }
  const wrap = (handler: () => unknown) => async (): Promise<[number, unknown]> => {
    return [200, handler()]
  }

  // 模块 mock 关闭时转发到真实 API（保留 baseURL，避免 mock 库 passThrough 丢 /api/admin 前缀）
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
  const modules: { key: string, register?: (m: MockAdapter, w: typeof wrap) => void, passthrough?: RegExp }[] = [
    { key: 'dashboard', register: registerDashboardMock },
    { key: 'permissions', register: registerPermissionMock },
    { key: 'applications', register: registerApplicationMock, passthrough: /^\/applications/ },
    { key: 'datasource', register: registerDatasourceMock },
    { key: 'analytics', register: registerAnalyticsMock },
    { key: 'profile', register: registerProfileMock },
    { key: 'apikey', register: registerApiKeyMock },
    { key: 'dubbing', register: registerDubbingMock },
    { key: 'orgUsers', register: registerOrgUsersMock },
    { key: 'roles', register: registerRolesMock },
    { key: 'standards', register: registerStandardsMock },
    { key: 'appOrder', passthrough: /^\/app-order/ },
  ]

  for (const mod of modules) {
    if (MOCK_MODULES[mod.key] === false) {
      // 模块 mock 关闭时直连真实 API（Vite 代理在 vite.config.ts 中配置）
      if (mod.passthrough) mock.onAny(mod.passthrough).reply(forwardToRealApi)
      continue
    }
    mod.register?.(mock, wrap)
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
}
