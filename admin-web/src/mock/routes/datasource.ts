import type MockAdapter from 'axios-mock-adapter'
import { mockDataSources } from '@/mock/data/data-sources'

/**
 * 注册数据源相关的 mock 路由
 */
export function registerDatasourceMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/datasources').reply(wrap(() => mockDataSources))
}
