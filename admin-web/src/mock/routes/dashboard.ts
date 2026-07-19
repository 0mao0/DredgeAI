import type MockAdapter from 'axios-mock-adapter'
import { mockAdminStats, mockMetrics, mockApiCallsTrend, mockAppDistribution, mockActiveUsersTrend, mockRecentLogs } from '@shared/mock/data/dashboard'

/**
 * 注册仪表盘相关的 mock 路由
 */
export function registerDashboardMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/api/admin/dashboard/stats').reply(wrap(() => mockAdminStats))
  mock.onGet('/api/admin/dashboard/metrics').reply(wrap(() => mockMetrics))
  mock.onGet('/api/admin/dashboard/api-calls-trend').reply(wrap(() => mockApiCallsTrend))
  mock.onGet('/api/admin/dashboard/app-distribution').reply(wrap(() => mockAppDistribution))
  mock.onGet('/api/admin/dashboard/active-users-trend').reply(wrap(() => mockActiveUsersTrend))
  mock.onGet('/api/admin/dashboard/recent-logs').reply(wrap(() => mockRecentLogs))
}
