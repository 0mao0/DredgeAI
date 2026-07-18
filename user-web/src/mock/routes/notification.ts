import type MockAdapter from 'axios-mock-adapter'
import { notifications } from '@/mock/data/notification'

export function registerNotificationMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/notification/list').reply(wrap(() => notifications))
}
