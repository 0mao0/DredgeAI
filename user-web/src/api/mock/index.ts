import MockAdapter from 'axios-mock-adapter'
import request from '@/api/request'
import { randomDelay } from '@/utils/request'
import { registerUserMock } from './modules/user'
import { registerAppMock } from './modules/app'
import { registerTaskMock } from './modules/task'
import { registerFileMock } from './modules/file'
import { registerBidMock } from './modules/bid'
import { registerStandardMock } from './modules/standard'
import { registerApiKeyMock } from './modules/apikey'
import { registerNotificationMock } from './modules/notification'

const USE_MOCK = true

export function registerMock(): void {
  if (!USE_MOCK) return

  const mock = new MockAdapter(request, { delayResponse: 0 })

  const wrap = (handler: () => unknown) => async () => {
    await randomDelay()
    return [200, { code: 0, data: handler(), message: 'ok' }]
  }

  registerUserMock(mock, wrap)
  registerAppMock(mock, wrap)
  registerTaskMock(mock, wrap)
  registerFileMock(mock, wrap)
  registerBidMock(mock, wrap)
  registerStandardMock(mock, wrap)
  registerApiKeyMock(mock, wrap)
  registerNotificationMock(mock, wrap)
}
