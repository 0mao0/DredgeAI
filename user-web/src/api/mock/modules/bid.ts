import type MockAdapter from 'axios-mock-adapter'
import { bidReviewSteps, riskItems, bidReviewSessions, bidDocumentExcerpt } from '@/mock/bid'

export function registerBidMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/bid/steps').reply(wrap(() => bidReviewSteps))
  mock.onGet('/bid/risks').reply(wrap(() => riskItems))
  mock.onGet('/bid/sessions').reply(wrap(() => bidReviewSessions))
  mock.onGet('/bid/document').reply(wrap(() => bidDocumentExcerpt))
}
