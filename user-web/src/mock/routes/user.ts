import type MockAdapter from 'axios-mock-adapter'
import { currentUser } from '@/mock/data/user'

export function registerUserMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/user/current').reply(wrap(() => currentUser))
}
