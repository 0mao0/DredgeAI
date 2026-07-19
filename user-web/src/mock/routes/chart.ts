import type MockAdapter from 'axios-mock-adapter'
import { efficiencyTrend } from '@shared/mock/data/chart'

export function registerChartMock(
  mock: MockAdapter,
  wrap: (h: () => unknown) => () => Promise<[number, unknown]>,
): void {
  mock.onGet('/api/chart/efficiency-trend').reply(wrap(() => efficiencyTrend))
}
