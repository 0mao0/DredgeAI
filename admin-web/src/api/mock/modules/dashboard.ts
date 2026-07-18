import type MockAdapter from 'axios-mock-adapter'
import { mockAdminStats, mockMetrics, mockApiCallsTrend, mockAppDistribution, mockActiveUsersTrend, mockRecentLogs } from '@/mock/dashboard'

export function registerDashboardMock(mock: MockAdapter, wrap: (handler: () => unknown) => () => Promise<[number, unknown]>): void {
  mock.onGet('/dashboard/stats').reply(wrap(() => mockAdminStats))
  mock.onGet('/dashboard/metrics').reply(wrap(() => mockMetrics))
  mock.onGet('/dashboard/api-calls-trend').reply(wrap(() => mockApiCallsTrend))
  mock.onGet('/dashboard/app-distribution').reply(wrap(() => mockAppDistribution))
  mock.onGet('/dashboard/active-users-trend').reply(wrap(() => mockActiveUsersTrend))
  mock.onGet('/dashboard/recent-logs').reply(wrap(() => mockRecentLogs))
}
