import type MockAdapter from 'axios-mock-adapter'
import { mockSystemLogs } from '@/mock/system-log'

export function registerSystemLogMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/system/logs').reply(wrap(() => mockSystemLogs))
}
