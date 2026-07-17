import type MockAdapter from 'axios-mock-adapter'
import { currentUser } from '@/mock/user'

export function registerUserMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/user/current').reply(wrap(() => currentUser))
}
