import request from '@/api/request'
import type { LineChartData } from '@/types'

export function getEfficiencyTrend(): Promise<LineChartData> {
  return request.get<LineChartData>('/chart/efficiency-trend')
}
