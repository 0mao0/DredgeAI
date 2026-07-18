import type MockAdapter from 'axios-mock-adapter'
import { mockSystemLogs } from '@/mock/data/system-log'

/**
 * 注册系统日志相关的 mock 路由
 */
export function registerSystemLogMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/system/logs').reply(wrap(() => mockSystemLogs))
}
