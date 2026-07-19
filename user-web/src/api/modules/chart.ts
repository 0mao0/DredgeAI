import request from '@/api/request'
import { urls } from '@shared/core/api'
import type { LineChartData } from '@/types'

export function getEfficiencyTrend(): Promise<LineChartData> {
  return request.get<LineChartData>(urls.chartEfficiencyTrend)
}
