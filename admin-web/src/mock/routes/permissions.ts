import type MockAdapter from 'axios-mock-adapter'
import { mockPermissions } from '@shared/mock/data/permissions'

/**
 * 注册权限管理相关�?mock 路由
 */
export function registerPermissionMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/permissions').reply(wrap(() => mockPermissions))
}
