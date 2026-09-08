import type MockAdapter from 'axios-mock-adapter'
import { mockDailyApiCalls, mockModelUsage, mockUserGrowth, mockErrorRate } from '@shared/mock/data/analytics'

/**
 * 注册数据分析相关�?mock 路由
 */
export function registerAnalyticsMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/analytics/daily-api-calls').reply(wrap(() => mockDailyApiCalls))
  mock.onGet('/api/analytics/model-usage').reply(wrap(() => mockModelUsage))
  mock.onGet('/api/analytics/user-growth').reply(wrap(() => mockUserGrowth))
  mock.onGet('/api/analytics/error-rate').reply(wrap(() => mockErrorRate))
}
