import type MockAdapter from 'axios-mock-adapter'
import { mockDataSources } from '@/mock/data-sources'

export function registerDatasourceMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/datasources').reply(wrap(() => mockDataSources))
}
