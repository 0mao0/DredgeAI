import type MockAdapter from 'axios-mock-adapter'
import { appCards } from '@shared/mock/data/app'

export function registerAppMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/bidcompare/app-catalog/list').reply(wrap(() => appCards))
}
