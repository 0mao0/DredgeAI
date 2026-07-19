import type MockAdapter from 'axios-mock-adapter'
import { bidReviewSteps, riskItems, bidReviewSessions, bidDocumentExcerpt } from '@shared/mock/data/bid'

export function registerBidMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/bid/steps').reply(wrap(() => bidReviewSteps))
  mock.onGet('/api/bid/risks').reply(wrap(() => riskItems))
  mock.onGet('/api/bid/sessions').reply(wrap(() => bidReviewSessions))
  mock.onGet('/api/bid/document').reply(wrap(() => bidDocumentExcerpt))
}
