import type MockAdapter from 'axios-mock-adapter'
import { mockProfile } from '@shared/mock/data/profile'

/**
 * 注册用户资料相关�?mock 路由
 */
export function registerProfileMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/profile').reply(wrap(() => mockProfile))
}
