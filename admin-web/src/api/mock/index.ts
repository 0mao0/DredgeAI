import MockAdapter from 'axios-mock-adapter'
import request, { randomDelay } from '@/api/request'
import { USE_MOCK } from '@/utils/constants'
import { registerDashboardMock } from './modules/dashboard'
import { registerPermissionMock } from './modules/permissions'
import { registerApplicationMock } from './modules/applications'
import { registerSystemLogMock } from './modules/system-log'
import { registerDatasourceMock } from './modules/datasource'
import { registerAnalyticsMock } from './modules/analytics'
import { registerProfileMock } from './modules/profile'

export function registerMock(): void {
  if (!USE_MOCK) return

  const mock = new MockAdapter(request, { delayResponse: 0 })

  const wrap = (handler: () => unknown) => async (): Promise<[number, unknown]> => {
    await randomDelay()
    return [200, { code: 0, data: handler(), message: 'ok' }]
  }

  registerDashboardMock(mock, wrap)
  registerPermissionMock(mock, wrap)
  registerApplicationMock(mock, wrap)
  registerSystemLogMock(mock, wrap)
  registerDatasourceMock(mock, wrap)
  registerAnalyticsMock(mock, wrap)
  registerProfileMock(mock, wrap)
}
