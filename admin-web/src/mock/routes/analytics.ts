import type MockAdapter from 'axios-mock-adapter'
import { mockDailyApiCalls, mockModelUsage, mockUserGrowth, mockErrorRate } from '@shared/mock/data/analytics'

/**
 * 注册数据分析相关�?mock 路由
 */
export function registerAnalyticsMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/analytics/daily-api-calls').reply(wrap(() => mockDailyApiCalls))
  mock.onGet('/api/admin/analytics/model-usage').reply(wrap(() => mockModelUsage))
  mock.onGet('/api/admin/analytics/user-growth').reply(wrap(() => mockUserGrowth))
  mock.onGet('/api/admin/analytics/error-rate').reply(wrap(() => mockErrorRate))
}
