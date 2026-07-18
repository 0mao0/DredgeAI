import type MockAdapter from 'axios-mock-adapter'
import { mockPermissions } from '@/mock/permissions'

export function registerPermissionMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/permissions').reply(wrap(() => mockPermissions))
}
