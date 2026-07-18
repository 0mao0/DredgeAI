import type MockAdapter from 'axios-mock-adapter'
import { mockPermissions } from '@/mock/data/permissions'

/**
 * 注册权限管理相关的 mock 路由
 */
export function registerPermissionMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/permissions').reply(wrap(() => mockPermissions))
}
