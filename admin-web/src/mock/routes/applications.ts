import type MockAdapter from 'axios-mock-adapter'
import { mockApplications } from '@/mock/data/applications'

/**
 * 注册应用管理相关的 mock 路由
 */
export function registerApplicationMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/applications').reply(wrap(() => mockApplications))
}
