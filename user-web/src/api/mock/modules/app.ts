import type MockAdapter from 'axios-mock-adapter'
import { appCards, categories } from '@/mock/app'

export function registerAppMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/app/list').reply(wrap(() => appCards))
  mock.onGet('/app/categories').reply(wrap(() => categories))
}
