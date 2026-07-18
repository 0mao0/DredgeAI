import type MockAdapter from 'axios-mock-adapter'
import { mockDailyApiCalls, mockModelUsage, mockUserGrowth, mockErrorRate } from '@/mock/analytics'

export function registerAnalyticsMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/analytics/daily-api-calls').reply(wrap(() => mockDailyApiCalls))
  mock.onGet('/analytics/model-usage').reply(wrap(() => mockModelUsage))
  mock.onGet('/analytics/user-growth').reply(wrap(() => mockUserGrowth))
  mock.onGet('/analytics/error-rate').reply(wrap(() => mockErrorRate))
}
