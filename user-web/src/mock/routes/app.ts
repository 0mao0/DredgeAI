import type MockAdapter from 'axios-mock-adapter'
import { appCards } from '@shared/mock/data/app'

export function registerAppMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/app/list').reply(wrap(() => appCards))
}
