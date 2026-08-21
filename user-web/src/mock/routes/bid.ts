import type MockAdapter from 'axios-mock-adapter'
import { bidReviewSessions } from '@shared/mock/data/bid'

export function registerBidMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/bid/sessions').reply(wrap(() => bidReviewSessions))
}
