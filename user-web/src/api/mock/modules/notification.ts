import type MockAdapter from 'axios-mock-adapter'
import { notifications } from '@/mock/notification'

export function registerNotificationMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/notification/list').reply(wrap(() => notifications))
}
